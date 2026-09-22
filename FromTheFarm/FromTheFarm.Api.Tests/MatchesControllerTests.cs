using FromTheFarm.Api.Controllers;
using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FromTheFarm.Api.Tests;

public class MatchesControllerTests
{
    private const string Farmer = "farmer-1";
    private const string Buyer = "buyer-1";
    private const string Stranger = "stranger-1";
    private const string MatchId = "listing-1:demand-1";

    // ---- Detail and contact gating -------------------------------------------------

    [Fact]
    public async Task GetMatchDetail_ReturnsNotFound_WhenTheMatchDoesNotExist()
    {
        var controller = ControllerFor(Farmer);

        var result = await controller.GetMatchDetail(MatchId);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetMatchDetail_ReturnsNotFound_ToSomeoneWhoIsNotInTheMatch()
    {
        // 404 rather than 403, so the response does not confirm the match exists.
        var controller = ControllerFor(Stranger, matches: new FakeRepository<MatchDocument>(AMatch("Confirmed")));

        var result = await controller.GetMatchDetail(MatchId);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Theory]
    [InlineData(Farmer)]
    [InlineData(Buyer)]
    public async Task GetMatchDetail_HidesContactDetails_WhileTheMatchIsOnlySuggested(string viewer)
    {
        var controller = ControllerFor(viewer, matches: new FakeRepository<MatchDocument>(AMatch("Suggested")), users: BothUsers());

        var detail = Detail(await controller.GetMatchDetail(MatchId));

        Assert.Equal("Suggested", detail.Status);
        Assert.Null(detail.CounterpartContact.DisplayName);
        Assert.Null(detail.CounterpartContact.Phone);
    }

    [Theory]
    [InlineData("Confirmed", Farmer, "Buyer Ben", "0832222222")]
    [InlineData("Confirmed", Buyer, "Farmer Fay", "0821111111")]
    [InlineData("Completed", Farmer, "Buyer Ben", "0832222222")]
    [InlineData("Completed", Buyer, "Farmer Fay", "0821111111")]
    public async Task GetMatchDetail_RevealsTheOtherPartysContact_OnceConfirmed(
        string status, string viewer, string expectedName, string expectedPhone)
    {
        var controller = ControllerFor(viewer, matches: new FakeRepository<MatchDocument>(AMatch(status)), users: BothUsers());

        var detail = Detail(await controller.GetMatchDetail(MatchId));

        Assert.Equal(expectedName, detail.CounterpartContact.DisplayName);
        Assert.Equal(expectedPhone, detail.CounterpartContact.Phone);
    }

    // ---- Confirm -------------------------------------------------------------------

    [Theory]
    [InlineData(Farmer)]
    [InlineData(Buyer)]
    public async Task ConfirmMatch_MovesSuggestedToConfirmed_ForEitherParty(string caller)
    {
        var matches = new FakeRepository<MatchDocument>(AMatch("Suggested"));
        var controller = ControllerFor(caller, matches: matches);
        var before = DateTime.UtcNow;

        var result = await controller.ConfirmMatch(MatchId);

        Assert.IsType<NoContentResult>(result);
        var stored = matches.Documents[MatchId];
        Assert.Equal("Confirmed", stored.Status);
        Assert.NotNull(stored.ConfirmedAt);
        Assert.InRange(stored.ConfirmedAt!.Value, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task ConfirmMatch_ReturnsNotFound_ToSomeoneWhoIsNotInTheMatch()
    {
        var matches = new FakeRepository<MatchDocument>(AMatch("Suggested"));
        var controller = ControllerFor(Stranger, matches: matches);

        var result = await controller.ConfirmMatch(MatchId);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, matches.WriteCount);
        Assert.Equal("Suggested", matches.Documents[MatchId].Status);
    }

    [Fact]
    public async Task ConfirmMatch_ReturnsNotFound_WhenTheMatchDoesNotExist()
    {
        var controller = ControllerFor(Farmer);

        var result = await controller.ConfirmMatch(MatchId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData("Confirmed")]
    [InlineData("Completed")]
    public async Task ConfirmMatch_IsRefused_UnlessTheMatchIsSuggested(string status)
    {
        var matches = new FakeRepository<MatchDocument>(AMatch(status));
        var controller = ControllerFor(Farmer, matches: matches);

        var result = await controller.ConfirmMatch(MatchId);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, matches.WriteCount);
        Assert.Equal(status, matches.Documents[MatchId].Status);
    }

    // ---- Complete ------------------------------------------------------------------

    [Theory]
    [InlineData(Farmer)]
    [InlineData(Buyer)]
    public async Task CompleteMatch_MovesConfirmedToCompleted_ForEitherParty(string caller)
    {
        var matches = new FakeRepository<MatchDocument>(AMatch("Confirmed"));
        var controller = ControllerFor(caller, matches: matches);
        var before = DateTime.UtcNow;

        var result = await controller.CompleteMatch(MatchId);

        Assert.IsType<NoContentResult>(result);
        var stored = matches.Documents[MatchId];
        Assert.Equal("Completed", stored.Status);
        Assert.NotNull(stored.CompletedAt);
        Assert.InRange(stored.CompletedAt!.Value, before, DateTime.UtcNow);
    }

    [Theory]
    [InlineData("Suggested")]
    [InlineData("Completed")]
    public async Task CompleteMatch_CannotSkipAStage_OrBeRepeated(string status)
    {
        var matches = new FakeRepository<MatchDocument>(AMatch(status));
        var controller = ControllerFor(Farmer, matches: matches);

        var result = await controller.CompleteMatch(MatchId);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, matches.WriteCount);
        Assert.Equal(status, matches.Documents[MatchId].Status);
    }

    [Fact]
    public async Task CompleteMatch_ReturnsNotFound_ToSomeoneWhoIsNotInTheMatch()
    {
        var matches = new FakeRepository<MatchDocument>(AMatch("Confirmed"));
        var controller = ControllerFor(Stranger, matches: matches);

        var result = await controller.CompleteMatch(MatchId);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal("Confirmed", matches.Documents[MatchId].Status);
    }

    // ---- Rating --------------------------------------------------------------------

    [Fact]
    public async Task SubmitRating_StoresTheRating_OnACompletedMatch()
    {
        var ratings = new FakeRepository<Rating>();
        var controller = ControllerFor(Farmer, matches: new FakeRepository<MatchDocument>(AMatch("Completed")));

        var result = await controller.SubmitRating(MatchId, new MatchesController.RatingRequest(true), ratings);

        Assert.IsType<CreatedAtActionResult>(result);
        var stored = Assert.Single(ratings.Documents.Values);
        Assert.Equal(MatchId, stored.MatchId);
        Assert.Equal(Farmer, stored.RaisedByUserId);
        Assert.True(stored.ThumbsUp);
    }

    [Theory]
    [InlineData("Suggested")]
    [InlineData("Confirmed")]
    public async Task SubmitRating_IsRefused_UntilTheMatchIsCompleted(string status)
    {
        var ratings = new FakeRepository<Rating>();
        var controller = ControllerFor(Farmer, matches: new FakeRepository<MatchDocument>(AMatch(status)));

        var result = await controller.SubmitRating(MatchId, new MatchesController.RatingRequest(true), ratings);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, ratings.WriteCount);
    }

    [Fact]
    public async Task SubmitRating_ReturnsConflict_ForASecondRatingFromTheSameUser()
    {
        var ratings = new FakeRepository<Rating>();
        var controller = ControllerFor(Farmer, matches: new FakeRepository<MatchDocument>(AMatch("Completed")));
        await controller.SubmitRating(MatchId, new MatchesController.RatingRequest(true), ratings);

        var second = await controller.SubmitRating(MatchId, new MatchesController.RatingRequest(false), ratings);

        Assert.IsType<ConflictObjectResult>(second);
        var stored = Assert.Single(ratings.Documents.Values);
        Assert.True(stored.ThumbsUp);
    }

    [Fact]
    public async Task SubmitRating_LetsTheOtherPartyRateTheSameMatch()
    {
        var ratings = new FakeRepository<Rating>();
        var matches = new FakeRepository<MatchDocument>(AMatch("Completed"));
        await ControllerFor(Farmer, matches: matches)
            .SubmitRating(MatchId, new MatchesController.RatingRequest(true), ratings);

        var result = await ControllerFor(Buyer, matches: matches)
            .SubmitRating(MatchId, new MatchesController.RatingRequest(false), ratings);

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(2, ratings.Documents.Count);
        Assert.Contains(ratings.Documents.Values, r => r.RaisedByUserId == Buyer && !r.ThumbsUp);
    }

    [Fact]
    public async Task SubmitRating_ReturnsNotFound_ToSomeoneWhoIsNotInTheMatch()
    {
        var ratings = new FakeRepository<Rating>();
        var controller = ControllerFor(Stranger, matches: new FakeRepository<MatchDocument>(AMatch("Completed")));

        var result = await controller.SubmitRating(MatchId, new MatchesController.RatingRequest(true), ratings);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, ratings.WriteCount);
    }

    [Fact]
    public async Task SubmitRating_ReturnsNotFound_WhenTheMatchDoesNotExist()
    {
        var ratings = new FakeRepository<Rating>();
        var controller = ControllerFor(Farmer);

        var result = await controller.SubmitRating(MatchId, new MatchesController.RatingRequest(true), ratings);

        Assert.IsType<NotFoundResult>(result);
    }

    // ---- Feed and match generation -------------------------------------------------

    [Fact]
    public async Task GetFeed_IsRefused_UntilTheCallerHasChosenARole()
    {
        var withoutRole = ControllerFor(Farmer, users: new FakeRepository<UserProfile>(AProfile(Farmer, role: null)));
        var withoutProfile = ControllerFor(Farmer);

        Assert.IsType<BadRequestObjectResult>((await withoutRole.GetFeed()).Result);
        Assert.IsType<BadRequestObjectResult>((await withoutProfile.GetFeed()).Result);
    }

    [Fact]
    public async Task GetFeed_CreatesAMatch_ForAFarmersListingAndANearbyDemandForTheSameCrop()
    {
        var matches = new FakeRepository<MatchDocument>();
        var controller = ControllerFor(Farmer, matches,
            users: new FakeRepository<UserProfile>(AProfile(Farmer, "Farmer")),
            listings: new FakeRepository<Listing>(AListing()),
            demands: new FakeRepository<DemandRequest>(ADemand()));

        var item = Assert.Single(Feed(await controller.GetFeed()));

        Assert.Equal(MatchId, item.MatchId);
        Assert.Equal(1.0, item.Score, precision: 3);
        Assert.Equal("Suggested", item.Status);
        Assert.Equal("Tomatoes", item.Counterpart.CropType);
        var stored = matches.Documents[MatchId];
        Assert.Equal(Farmer, stored.FarmerId);
        Assert.Equal(Buyer, stored.BuyerId);
    }

    [Fact]
    public async Task GetFeed_CreatesTheSameMatch_FromTheBuyersSide()
    {
        var matches = new FakeRepository<MatchDocument>();
        var controller = ControllerFor(Buyer, matches,
            users: new FakeRepository<UserProfile>(AProfile(Buyer, "Buyer")),
            listings: new FakeRepository<Listing>(AListing()),
            demands: new FakeRepository<DemandRequest>(ADemand()));

        var item = Assert.Single(Feed(await controller.GetFeed()));

        Assert.Equal(MatchId, item.MatchId);
        Assert.Equal(Farmer, matches.Documents[MatchId].FarmerId);
    }

    [Theory]
    [InlineData("Onions", "Open", -29.85, 31.02)]      // different crop
    [InlineData("Tomatoes", "Expired", -29.85, 31.02)] // demand no longer open
    [InlineData("Tomatoes", "Open", -26.20, 28.04)]    // roughly 500km away, outside the 10km radius
    public async Task GetFeed_CreatesNoMatch_WhenTheDemandDoesNotQualify(
        string crop, string demandStatus, double latitude, double longitude)
    {
        var matches = new FakeRepository<MatchDocument>();
        var controller = ControllerFor(Farmer, matches,
            users: new FakeRepository<UserProfile>(AProfile(Farmer, "Farmer")),
            listings: new FakeRepository<Listing>(AListing()),
            demands: new FakeRepository<DemandRequest>(ADemand(crop: crop, status: demandStatus, lat: latitude, lon: longitude)));

        var feed = Feed(await controller.GetFeed());

        Assert.Empty(feed);
        Assert.Equal(0, matches.WriteCount);
    }

    [Fact]
    public async Task GetFeed_IgnoresAListingThatHasBeenDeleted()
    {
        var matches = new FakeRepository<MatchDocument>();
        var controller = ControllerFor(Farmer, matches,
            users: new FakeRepository<UserProfile>(AProfile(Farmer, "Farmer")),
            listings: new FakeRepository<Listing>(AListing(status: "Deleted")),
            demands: new FakeRepository<DemandRequest>(ADemand()));

        Assert.Empty(Feed(await controller.GetFeed()));
        Assert.Equal(0, matches.WriteCount);
    }

    [Fact]
    public async Task GetFeed_ReturnsOnlyTheCallersMatches_BestFirst()
    {
        var matches = new FakeRepository<MatchDocument>(
            AMatch("Suggested", id: "low", score: 0.5),
            AMatch("Suggested", id: "high", score: 0.9),
            AMatch("Suggested", id: "someone-elses", score: 0.99, farmer: "other-farmer", buyer: "other-buyer"));
        var controller = ControllerFor(Farmer, matches,
            users: new FakeRepository<UserProfile>(AProfile(Farmer, "Farmer")));

        var feed = Feed(await controller.GetFeed());

        Assert.Equal(new[] { "high", "low" }, feed.Select(m => m.MatchId).ToArray());
    }

    [Fact]
    public async Task GetFeed_IsIdempotent_AndNeverResetsAMatchThatHasMovedOn()
    {
        var matches = new FakeRepository<MatchDocument>(AMatch("Confirmed", score: 0.2));
        var controller = ControllerFor(Farmer, matches,
            users: new FakeRepository<UserProfile>(AProfile(Farmer, "Farmer")),
            listings: new FakeRepository<Listing>(AListing()),
            demands: new FakeRepository<DemandRequest>(ADemand()));

        await controller.GetFeed();
        await controller.GetFeed();

        Assert.Single(matches.Documents);
        var stored = matches.Documents[MatchId];
        Assert.Equal("Confirmed", stored.Status);
        Assert.Equal(1.0, stored.Score, precision: 3);
    }

    // ---- Helpers -------------------------------------------------------------------

    private static MatchesController ControllerFor(
        string uid,
        FakeRepository<MatchDocument>? matches = null,
        FakeRepository<UserProfile>? users = null,
        FakeRepository<Listing>? listings = null,
        FakeRepository<DemandRequest>? demands = null) =>
        new(
            users ?? new FakeRepository<UserProfile>(),
            listings ?? new FakeRepository<Listing>(),
            demands ?? new FakeRepository<DemandRequest>(),
            matches ?? new FakeRepository<MatchDocument>(),
            new MatchingService())
        {
            ControllerContext = SignedInAs.User(uid)
        };

    private static MatchesController.MatchDetailResponse Detail(ActionResult<MatchesController.MatchDetailResponse> result) =>
        Assert.IsType<MatchesController.MatchDetailResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static List<MatchesController.MatchFeedItem> Feed(ActionResult<List<MatchesController.MatchFeedItem>> result) =>
        Assert.IsType<List<MatchesController.MatchFeedItem>>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static FakeRepository<UserProfile> BothUsers() => new(
        AProfile(Farmer, "Farmer", "Farmer Fay", "0821111111"),
        AProfile(Buyer, "Buyer", "Buyer Ben", "0832222222"));

    private static UserProfile AProfile(string uid, string? role, string name = "Someone", string? phone = null) =>
        new() { Id = uid, UserId = uid, Role = role, DisplayName = name, Phone = phone };

    private static MatchDocument AMatch(
        string status,
        string id = MatchId,
        double score = 0.9,
        string farmer = Farmer,
        string buyer = Buyer) =>
        new()
        {
            Id = id,
            ListingId = "listing-1",
            DemandRequestId = "demand-1",
            FarmerId = farmer,
            BuyerId = buyer,
            Score = score,
            Status = status
        };

    private static Listing AListing(string status = "Active") => new()
    {
        Id = "listing-1",
        FarmerId = Farmer,
        CropType = "Tomatoes",
        Quantity = 100,
        Unit = "kg",
        HarvestDate = new DateOnly(2026, 10, 1),
        Location = new GeoLocation { Latitude = -29.85, Longitude = 31.02 },
        Status = status
    };

    private static DemandRequest ADemand(
        string crop = "Tomatoes", string status = "Open", double lat = -29.85, double lon = 31.02) => new()
    {
        Id = "demand-1",
        BuyerId = Buyer,
        CropType = crop,
        QuantityNeeded = 100,
        Unit = "kg",
        Deadline = new DateOnly(2026, 10, 10),
        Location = new GeoLocation { Latitude = lat, Longitude = lon },
        Status = status
    };
}
