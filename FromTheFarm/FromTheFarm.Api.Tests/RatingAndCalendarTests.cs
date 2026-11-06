using FromTheFarm.Api.Controllers;
using FromTheFarm.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FromTheFarm.Api.Tests;

public class RatingAndCalendarTests
{
    private const string MatchId = "listing-1:demand-1";
    private const string Farmer = "farmer-1";
    private const string Buyer = "buyer-1";
    private const string Stranger = "stranger-1";

    // ---- GET rating ----------------------------------------------------------------

    [Fact]
    public async Task GetRating_ReturnsNoContent_BeforeTheCallerHasRated()
    {
        var result = await RatingController(Farmer).GetRating(MatchId, new FakeRepository<Rating>());

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task GetRating_ReturnsTheCallersOwnRating_AndNotTheOtherPartys()
    {
        var ratings = new FakeRepository<Rating>(
            new Rating { MatchId = MatchId, RaisedByUserId = Farmer, ThumbsUp = true },
            new Rating { MatchId = MatchId, RaisedByUserId = Buyer, ThumbsUp = false });

        var asFarmer = Assert.IsType<OkObjectResult>(await RatingController(Farmer).GetRating(MatchId, ratings));
        var asBuyer = Assert.IsType<OkObjectResult>(await RatingController(Buyer).GetRating(MatchId, ratings));

        Assert.True(Assert.IsType<MatchesController.RatingResponse>(asFarmer.Value).ThumbsUp);
        Assert.False(Assert.IsType<MatchesController.RatingResponse>(asBuyer.Value).ThumbsUp);
    }

    [Fact]
    public async Task GetRating_IsNotFound_ForSomeoneOutsideTheMatch()
    {
        var ratings = new FakeRepository<Rating>(
            new Rating { MatchId = MatchId, RaisedByUserId = Farmer, ThumbsUp = true });

        var result = await RatingController(Stranger).GetRating(MatchId, ratings);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetRating_IsNotFound_ForAnUnknownMatch()
    {
        var result = await RatingController(Farmer).GetRating("nope", new FakeRepository<Rating>());

        Assert.IsType<NotFoundResult>(result);
    }

    // ---- GET calendar --------------------------------------------------------------

    [Fact]
    public async Task Calendar_GroupsActiveListingsByHarvestDate_WithinTheRequestedMonth()
    {
        var controller = CalendarFor(Buyer,
            Listing("a", "Tomatoes", "2026-09-10"),
            Listing("b", "Maize", "2026-09-10"),
            Listing("c", "Beans", "2026-09-25"),
            Listing("d", "Potatoes", "2026-10-01"),
            Listing("e", "Onions", "2026-08-31"),
            Listing("f", "Spinach", "2026-09-12", status: "Deleted"));

        var response = Body(await controller.GetCalendar(9, 2026));

        Assert.Equal(new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 25) }, response.Days.Select(d => d.Date));
        Assert.Equal(new[] { "Maize", "Tomatoes" }, response.Days[0].Listings.Select(l => l.CropType));
    }

    [Fact]
    public async Task Calendar_MarksTheCallersOwnListings_AndExposesNoOwnerOrLocation()
    {
        var controller = CalendarFor(Farmer,
            Listing("mine", "Tomatoes", "2026-09-10", farmer: Farmer),
            Listing("theirs", "Maize", "2026-09-10", farmer: "other"));

        var listings = Body(await controller.GetCalendar(9, 2026)).Days.Single().Listings;

        Assert.True(listings.Single(l => l.ListingId == "mine").IsMine);
        Assert.False(listings.Single(l => l.ListingId == "theirs").IsMine);
        Assert.DoesNotContain(typeof(CalendarController.CalendarListing).GetProperties(),
            p => p.Name is "FarmerId" or "Location");
    }

    [Fact]
    public async Task Calendar_IsEmpty_ForAMonthWithNoHarvests()
    {
        var response = Body(await CalendarFor(Buyer, Listing("a", "Tomatoes", "2026-09-10")).GetCalendar(1, 2027));

        Assert.Empty(response.Days);
    }

    [Theory]
    [InlineData(0, 2026)]
    [InlineData(13, 2026)]
    [InlineData(5, 1999)]
    [InlineData(5, 2101)]
    public async Task Calendar_RejectsAnOutOfRangeMonthOrYear(int month, int year)
    {
        var result = await CalendarFor(Buyer).GetCalendar(month, year);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Calendar_DefaultsToTheCurrentMonth()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = Body(await CalendarFor(Buyer).GetCalendar());

        Assert.Equal(today.Month, response.Month);
        Assert.Equal(today.Year, response.Year);
    }

    // ---- helpers -------------------------------------------------------------------

    private static MatchesController RatingController(string uid) =>
        new(new FakeRepository<UserProfile>(),
            new FakeRepository<MatchDocument>(new MatchDocument
            {
                Id = MatchId, ListingId = "listing-1", DemandRequestId = "demand-1",
                FarmerId = Farmer, BuyerId = Buyer, Status = "Completed"
            }),
            Matching.Service(), Matching.Notifications())
        {
            ControllerContext = SignedInAs.User(uid)
        };

    private static CalendarController CalendarFor(string uid, params Listing[] listings) =>
        new(new FakeRepository<Listing>(listings)) { ControllerContext = SignedInAs.User(uid) };

    private static Listing Listing(string id, string crop, string harvest, string status = "Active", string farmer = "farmer-x") =>
        new()
        {
            Id = id, CropType = crop, Quantity = 10, Unit = "kg", Status = status,
            FarmerId = farmer, HarvestDate = DateOnly.Parse(harvest)
        };

    private static CalendarController.CalendarResponse Body(ActionResult<CalendarController.CalendarResponse> result) =>
        Assert.IsType<CalendarController.CalendarResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
