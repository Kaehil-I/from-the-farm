using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace FromTheFarm.Api.Controllers;

[ApiController]
[Route("api/v1/listings")]
[Authorize]
public class ListingsController : ControllerBase
{
    private readonly IMongoRepository<Listing> _listings;
    private readonly IMongoRepository<UserProfile> _users;
    private readonly MatchGenerationService _matching;
    private readonly IPhotoStore? _photos;

    // photos is optional so a caller with no blob storage keeps the original
    // behaviour of holding the image inline on the document. The deployed API
    // always registers a store.
    public ListingsController(
        IMongoRepository<Listing> listings,
        IMongoRepository<UserProfile> users,
        MatchGenerationService matching,
        IPhotoStore? photos = null)
    {
        _listings = listings;
        _users = users;
        _matching = matching;
        _photos = photos;
    }

    // Turns a validated upload into the value a listing keeps in PhotoUrl: a link
    // to the stored blob, or an inline data URI when there is no store. Null means
    // no photo was supplied.
    private async Task<string?> StorePhotoAsync(string? photoBase64)
    {
        RequestValidation.DecodePhoto(photoBase64, out var bytes, out var mediaType);
        if (bytes is null || mediaType is null)
        {
            return null;
        }
        if (_photos is null)
        {
            return $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
        }

        var id = await _photos.SaveAsync(bytes, mediaType);
        return PhotoLinks.For(Request, id);
    }

    public record CreateListingRequest(
        string? ClientGeneratedId,
        string CropType,
        decimal Quantity,
        string Unit,
        DateOnly HarvestDate,
        GeoLocation Location,
        string? PhotoBase64);

    // Section 5 specifies maxDistanceKm as a query parameter but does not state
    // what location it is measured from for a browsing buyer, as opposed to one
    // with an existing demand request. Resolved pragmatically: latitude and
    // longitude are accepted as optional query parameters carrying the app's
    // current GPS fix, and distance filtering is skipped when they are omitted.
    [HttpGet]
    public async Task<ActionResult<List<Listing>>> GetListings(
        [FromQuery] bool mine = false,
        [FromQuery] string? cropType = null,
        [FromQuery] int? maxDistanceKm = null,
        [FromQuery] double? latitude = null,
        [FromQuery] double? longitude = null)
    {
        var uid = User.GetFirebaseUid();
        var query = _listings.Collection.AsQueryable()
            .Where(l => l.Status == "Active");

        query = mine ? query.Where(l => l.FarmerId == uid) : query;
        query = cropType is not null ? query.Where(l => l.CropType == cropType) : query;

        var results = await query.ToListAsync();

        if (!mine && maxDistanceKm is not null && latitude is not null && longitude is not null)
        {
            results = results
                .Where(l => MatchingService.CalculateDistanceKm(
                    latitude.Value, longitude.Value, l.Location.Latitude, l.Location.Longitude) <= maxDistanceKm.Value)
                .ToList();
        }

        // Browsing shows other farms; only the owner's own view keeps exact data.
        if (!mine)
        {
            results = results.Select(BrowsePrivacy.Redact).ToList();
        }

        return Ok(results);
    }

    [HttpPost]
    public async Task<ActionResult<Listing>> CreateListing([FromBody] CreateListingRequest request)
    {
        // Authorisation comes before validation: a caller who may not create a
        // listing at all should not learn anything about what a valid one looks like.
        var uid = User.GetFirebaseUid();
        var profile = await _users.GetByIdAsync(uid);
        var notAllowed = RoleRequirement.Check(profile, "Farmer", "create listings");
        if (notAllowed is not null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, notAllowed);
        }

        var invalid = RequestValidation.ForListing(request.CropType, request.Quantity, request.Unit, request.Location);
        if (invalid is not null)
        {
            return BadRequest(invalid);
        }

        var photoError = RequestValidation.DecodePhoto(request.PhotoBase64, out _, out _);
        if (photoError is not null)
        {
            return BadRequest(photoError);
        }

        // A create can be retried: the device was offline, the request timed out,
        // or the response was lost on the way back. When the client supplies an id
        // it generated, a record already carrying that id for this farmer is the
        // same record rather than a second one, so return it untouched instead of
        // inserting a duplicate. Without this the queue would create a new listing
        // on every retry.
        if (!string.IsNullOrWhiteSpace(request.ClientGeneratedId))
        {
            var alreadyCreated = await _listings.FindAsync(
                l => l.FarmerId == uid && l.ClientGeneratedId == request.ClientGeneratedId);

            if (alreadyCreated.Count > 0)
            {
                return Ok(alreadyCreated[0]);
            }
        }

        var listing = new Listing
        {
            FarmerId = uid,
            ClientGeneratedId = request.ClientGeneratedId,
            CropType = request.CropType,
            Quantity = request.Quantity,
            Unit = request.Unit,
            HarvestDate = request.HarvestDate,
            Location = request.Location,
            // The image goes to blob storage and the listing keeps only its URL.
            PhotoUrl = await StorePhotoAsync(request.PhotoBase64)
        };

        var created = await _listings.UpsertAsync(listing);

        // Match straight away so buyers with a matching demand hear about it now
        // rather than whenever somebody next opens their feed.
        await _matching.OnListingCreatedAsync(created, profile!.SearchRadiusKm);

        return CreatedAtAction(nameof(GetListings), new { }, created);
    }

    [HttpPut("{listingId}")]
    public async Task<ActionResult<Listing>> UpdateListing(string listingId, [FromBody] CreateListingRequest request)
    {
        var invalid = RequestValidation.ForListing(request.CropType, request.Quantity, request.Unit, request.Location);
        if (invalid is not null)
        {
            return BadRequest(invalid);
        }

        var photoError = RequestValidation.DecodePhoto(request.PhotoBase64, out _, out _);
        if (photoError is not null)
        {
            return BadRequest(photoError);
        }

        var uid = User.GetFirebaseUid();
        var existing = await _listings.GetByIdAsync(listingId);
        if (existing is null)
        {
            return NotFound();
        }

        // The token identifies the caller; the document records its owner. Without
        // this check any authenticated user who knows a listing id could edit it.
        if (!string.Equals(existing.FarmerId, uid, StringComparison.Ordinal))
        {
            return Forbid();
        }

        existing.CropType = request.CropType;
        existing.Quantity = request.Quantity;
        existing.Unit = request.Unit;
        existing.HarvestDate = request.HarvestDate;
        existing.Location = request.Location;

        // The app only sends a photo when the user picks a new one, so an absent
        // payload means "keep the current image" rather than "remove it".
        var newPhotoUrl = await StorePhotoAsync(request.PhotoBase64);
        if (newPhotoUrl is not null)
        {
            var replacedId = PhotoLinks.IdFrom(existing.PhotoUrl);
            existing.PhotoUrl = newPhotoUrl;
            if (replacedId is not null && _photos is not null)
            {
                await _photos.DeleteAsync(replacedId);
            }
        }

        var updated = await _listings.UpsertAsync(existing);

        // An edit can make the listing match different demands (new crop, new
        // location); suggestions it no longer fits are dropped when feeds load.
        var radius = (await _users.GetByIdAsync(uid))?.SearchRadiusKm ?? 10;
        await _matching.OnListingCreatedAsync(updated, radius);

        return Ok(updated);
    }

    [HttpDelete("{listingId}")]
    public async Task<IActionResult> DeleteListing(string listingId)
    {
        var uid = User.GetFirebaseUid();
        var existing = await _listings.GetByIdAsync(listingId);
        if (existing is null)
        {
            return NotFound();
        }

        if (!string.Equals(existing.FarmerId, uid, StringComparison.Ordinal))
        {
            return Forbid();
        }

        // Soft-delete: preserves history for any matches already generated
        // against this listing, per Section 5.
        existing.Status = "Deleted";

        // A deleted listing has no use for its photo, so free the stored blob.
        var photoId = PhotoLinks.IdFrom(existing.PhotoUrl);
        if (photoId is not null && _photos is not null)
        {
            await _photos.DeleteAsync(photoId);
            existing.PhotoUrl = null;
        }
        await _listings.UpsertAsync(existing);
        return NoContent();
    }
}
