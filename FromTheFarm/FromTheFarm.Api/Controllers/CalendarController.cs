using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FromTheFarm.Api.Controllers;

// Harvest calendar (design section 5.3.7). There is no calendar collection:
// this groups existing Active listings by expected harvest date, so there is no
// second write path to keep in sync.
[ApiController]
[Route("api/v1/calendar")]
[Authorize]
public class CalendarController : ControllerBase
{
    private readonly IMongoRepository<Listing> _listings;

    public CalendarController(IMongoRepository<Listing> listings)
    {
        _listings = listings;
    }

    // Only what a calendar cell needs. No coordinates and no owner id, so this
    // is safe to show for other people's listings; IsMine lets the caller's own
    // produce be highlighted.
    public record CalendarListing(string ListingId, string CropType, decimal Quantity, string Unit, bool IsMine);
    public record CalendarDay(DateOnly Date, List<CalendarListing> Listings);
    public record CalendarResponse(int Month, int Year, List<CalendarDay> Days);

    [HttpGet]
    public async Task<ActionResult<CalendarResponse>> GetCalendar(
        [FromQuery] int? month = null, [FromQuery] int? year = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var m = month ?? today.Month;
        var y = year ?? today.Year;

        if (m < 1 || m > 12)
        {
            return BadRequest("month must be between 1 and 12.");
        }
        if (y < 2000 || y > 2100)
        {
            return BadRequest("year must be between 2000 and 2100.");
        }

        var uid = User.GetFirebaseUid();
        var first = new DateOnly(y, m, 1);
        var last = first.AddMonths(1).AddDays(-1);

        var found = await _listings.FindAsync(l =>
            l.Status == "Active" && l.HarvestDate >= first && l.HarvestDate <= last);

        var days = found
            .GroupBy(l => l.HarvestDate)
            .OrderBy(g => g.Key)
            .Select(g => new CalendarDay(
                g.Key,
                g.OrderBy(l => l.CropType, StringComparer.OrdinalIgnoreCase)
                 .Select(l => new CalendarListing(l.Id, l.CropType, l.Quantity, l.Unit, l.FarmerId == uid))
                 .ToList()))
            .ToList();

        return Ok(new CalendarResponse(m, y, days));
    }
}
