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
    private readonly MongoRepository<Listing> _listings;

    public ListingsController(MongoRepository<Listing> listings)
    {
        _listings = listings;
    }

    public record CreateListingRequest(
        string? ClientGeneratedId,
        string CropType,
        decimal Quantity,
        string Unit,
        DateOnly HarvestDate,
        GeoLocation Location,
        string? PhotoBase64);

    // NOTE: Section 5 specifies maxDistanceKm as a query parameter but the
    // design doc doesn't state what location it's measured from for a
    // *browsing* buyer (as opposed to an already-created demand request).
    // Resolved here pragmatically: latitude/longitude are accepted as
    // optional query params supplied by the app's current GPS fix; distance
    // filtering is skipped if they're omitted. Flag this in the AI usage /
    // design notes if asked, since it's a gap-fill rather than a literal
    // implementation of the written spec.
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
        if (string.IsNullOrWhiteSpace(request.CropType) || request.Quantity <= 0)
        {
            return BadRequest("cropType is required and quantity must be greater than 0.");
        }

        var uid = User.GetFirebaseUid();
        var listing = new Listing
        {
            FarmerId = uid,
            ClientGeneratedId = request.ClientGeneratedId,
            CropType = request.CropType,
            Quantity = request.Quantity,
            Unit = request.Unit,
            HarvestDate = request.HarvestDate,
            Location = request.Location,
            // Gap identified during Zario's review: this previously discarded
            // photoBase64 entirely, silently breaking the photo feature.
            // Stopgap for Part 2: store it as a data: URI directly on the
            // document so photos actually round-trip and render in the app.
            // This is NOT the final design — the POE's dedicated object
            // storage task replaces this with a real upload and swaps PhotoUrl
            // to a hosted URL. Flagging now so it doesn't look like an
            // oversight when that task starts: MongoDB documents cap at 16MB,
            // so this holds for small or compressed images, but it is not how
            // production should store photos.
            PhotoUrl = string.IsNullOrEmpty(request.PhotoBase64)
                ? null
                : $"data:image/jpeg;base64,{request.PhotoBase64}"
        };

        var created = await _listings.UpsertAsync(listing);
        return CreatedAtAction(nameof(GetListings), new { }, created);
    }

    [HttpPut("{listingId}")]
    public async Task<ActionResult<Listing>> UpdateListing(string listingId, [FromBody] CreateListingRequest request)
    {
        var uid = User.GetFirebaseUid();
        var existing = await _listings.GetByIdAsync(listingId);
        if (existing is null) return NotFound();

        existing.CropType = request.CropType;
        existing.Quantity = request.Quantity;
        existing.Unit = request.Unit;
        existing.HarvestDate = request.HarvestDate;
        existing.Location = request.Location;

        var updated = await _listings.UpsertAsync(existing);
        return Ok(updated);
    }

    [HttpDelete("{listingId}")]
    public async Task<IActionResult> DeleteListing(string listingId)
    {
        var uid = User.GetFirebaseUid();
        var existing = await _listings.GetByIdAsync(listingId);
        if (existing is null) return NotFound();

        // Soft-delete: preserves history for any matches already generated
        // against this listing, per Section 5.
        existing.Status = "Deleted";
        await _listings.UpsertAsync(existing);
        return NoContent();
    }
}
