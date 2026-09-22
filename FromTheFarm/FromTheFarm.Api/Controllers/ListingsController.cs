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

    public ListingsController(IMongoRepository<Listing> listings, IMongoRepository<UserProfile> users)
    {
        _listings = listings;
        _users = users;
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

        return Ok(results);
    }

    [HttpPost]
    public async Task<ActionResult<Listing>> CreateListing([FromBody] CreateListingRequest request)
    {
        // Authorisation comes before validation: a caller who may not create a
        // listing at all should not learn anything about what a valid one looks like.
        var uid = User.GetFirebaseUid();
        var notAllowed = RoleRequirement.Check(await _users.GetByIdAsync(uid), "Farmer", "create listings");
        if (notAllowed is not null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, notAllowed);
        }

        var invalid = RequestValidation.ForListing(request.CropType, request.Quantity, request.Unit, request.Location);
        if (invalid is not null)
        {
            return BadRequest(invalid);
        }

        var photoError = RequestValidation.Photo(request.PhotoBase64, out var photoDataUri);
        if (photoError is not null)
        {
            return BadRequest(photoError);
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
            // Part 2 stores the image inline on the document as a data URI so
            // photos round-trip without a separate upload step. The POE's object
            // storage task replaces this with a hosted URL; a Mongo document is
            // capped at 16MB, so this holds for compressed phone photos only.
            PhotoUrl = photoDataUri
        };

        var created = await _listings.UpsertAsync(listing);
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

        var photoError = RequestValidation.Photo(request.PhotoBase64, out var photoDataUri);
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
        if (photoDataUri is not null)
        {
            existing.PhotoUrl = photoDataUri;
        }

        var updated = await _listings.UpsertAsync(existing);
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
        await _listings.UpsertAsync(existing);
        return NoContent();
    }
}
