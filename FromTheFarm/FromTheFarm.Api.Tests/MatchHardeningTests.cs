using FromTheFarm.Api.Controllers;
using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FromTheFarm.Api.Tests;

// Behaviour that keeps stale, self-referential or expired records out of matching
// and keeps other people's exact locations out of browse results.
public class MatchHardeningTests
{
    private const string Farmer = "farmer-1";
    private const string Buyer = "buyer-1";
    private const string MatchId = "listing-1:demand-1";

    private static readonly DateOnly Future = new(2099, 12, 31);
    private static readonly DateOnly Past = new(2000, 1, 1);

    // ---- Confirming ----------------------------------------------------------------

    [Fact]
    public async Task ConfirmMatch_IsRefused_AndTheSuggestionRemoved_WhenTheListingWasDeleted()
    {
        var push = new FakePushSender();
        var matches = new FakeRepository<MatchDocument>(AMatch("Suggested"));
        var controller = MatchesFor(Buyer, matches, listing: AListing(status: "Deleted"), demand: ADemand(), push: push);

        var result = await controller.ConfirmMatch(MatchId);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Empty(matches.Documents);
        Assert.Empty(push.Sent);
    }

    [Theory]
    [InlineData("Expired", 2099)]
    [InlineData("Open", 2000)]
    public async Task ConfirmMatch_IsRefused_WhenTheDemandIsClosedOrPastItsDeadline(string status, int deadlineYear)
    {
        var matches = new FakeRepository<MatchDocument>(AMatch("Suggested"));
        var demand = ADemand(status: status, deadline: new DateOnly(deadlineYear, 1, 1));
        var controller = MatchesFor(Farmer, matches, listing: AListing(), demand: demand);

        var result = await controller.ConfirmMatch(MatchId);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Empty(matches.Documents);
    }

    [Fact]
    public async Task ConfirmMatch_IsRefused_ForAUserMatchedWithTheirOwnRecords()
    {
        var matches = new FakeRepository<MatchDocument>(AMatch("Suggested", farmer: Farmer, buyer: Farmer));
        var controller = MatchesFor(Farmer, matches, listing: AListing(), demand: ADemand(buyer: Farmer));

        var result = await controller.ConfirmMatch(MatchId);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Empty(matches.Documents);
    }

    // ---- The feed ------------------------------------------------------------------

    [Fact]
    public async Task GetFeed_DropsSuggestionsWhoseListingWasDeleted()
    {
        var matches = new FakeRepository<MatchDocument>(AMatch("Suggested"));
        var controller = MatchesFor(Buyer, matches, listing: AListing(status: "Deleted"), demand: ADemand(),
            users: new FakeRepository<UserProfile>(AProfile(Buyer, "Buyer")));

        var feed = Feed(await controller.GetFeed());

        Assert.Empty(feed);
        Assert.Empty(matches.Documents);
    }

    [Theory]
    [InlineData("Confirmed")]
    [InlineData("Completed")]
    public async Task GetFeed_KeepsAnAgreedExchange_EvenAfterTheListingIsDeleted(string status)
    {
        var matches = new FakeRepository<MatchDocument>(AMatch(status));
        var controller = MatchesFor(Buyer, matches, listing: AListing(status: "Deleted"), demand: ADemand(),
            users: new FakeRepository<UserProfile>(AProfile(Buyer, "Buyer")));

        var feed = Feed(await controller.GetFeed());

        Assert.Equal(MatchId, Assert.Single(feed).MatchId);
    }

    [Fact]
    public async Task GetFeed_DropsAMatchBetweenAUsersOwnListingAndDemand()
    {
        var matches = new FakeRepository<MatchDocument>(AMatch("Suggested", farmer: Farmer, buyer: Farmer));
        var controller = MatchesFor(Farmer, matches, listing: AListing(), demand: ADemand(buyer: Farmer),
            users: new FakeRepository<UserProfile>(AProfile(Farmer, "Farmer")));

        Assert.Empty(Feed(await controller.GetFeed()));
    }

    [Fact]
    public async Task GetFeed_NeverCreatesAMatchBetweenAUsersOwnListingAndDemand()
    {
        var matches = new FakeRepository<MatchDocument>();
        var controller = MatchesFor(Farmer, matches, listing: AListing(), demand: ADemand(buyer: Farmer),
            users: new FakeRepository<UserProfile>(AProfile(Farmer, "Farmer")));

        await controller.GetFeed();

        Assert.Empty(matches.Documents);
    }

    // ---- Matching rules ------------------------------------------------------------

    [Fact]
    public async Task GetFeed_DoesNotMatchAgainstADemandPastItsDeadline()
    {
        var matches = new FakeRepository<MatchDocument>();
        var controller = MatchesFor(Farmer, matches, listing: AListing(), demand: ADemand(deadline: Past),
            users: new FakeRepository<UserProfile>(AProfile(Farmer, "Farmer")));

        Assert.Empty(Feed(await controller.GetFeed()));
        Assert.Empty(matches.Documents);
    }

    [Fact]
    public async Task PostingAListing_DoesNotNotifyABuyerWhoseDemandHasExpired()
    {
        var push = new FakePushSender();
        var users = new FakeRepository<UserProfile>(AFarmerWithPhone(), ABuyerWithPhone());
        var listings = new FakeRepository<Listing>();
        var demands = new FakeRepository<DemandRequest>(ADemand(deadline: Past));
        var matches = new FakeRepository<MatchDocument>();
        var controller = new ListingsController(listings, users, Matching.Service(listings, demands, matches, users, push))
        {
            ControllerContext = SignedInAs.User(Farmer)
        };

        await controller.CreateListing(new ListingsController.CreateListingRequest(
            null, "Tomatoes", 50, "kg", new DateOnly(2026, 10, 1), new GeoLocation { Latitude = -29.85, Longitude = 31.02 }, null));

        Assert.Empty(matches.Documents);
        Assert.Empty(push.Sent);
    }

    // ---- Edits create the matches they now deserve ---------------------------------

    [Fact]
    public async Task EditingAListingToTheCropABuyerWants_CreatesAMatchAndNotifiesThem()
    {
        var push = new FakePushSender();
        var users = new FakeRepository<UserProfile>(AFarmerWithPhone(), ABuyerWithPhone());
        var listing = AListing(crop: "Maize");
        var listings = new FakeRepository<Listing>(listing);
        var demands = new FakeRepository<DemandRequest>(ADemand());
        var matches = new FakeRepository<MatchDocument>();
        var controller = new ListingsController(listings, users, Matching.Service(listings, demands, matches, users, push))
        {
            ControllerContext = SignedInAs.User(Farmer)
        };

        await controller.UpdateListing(listing.Id, new ListingsController.CreateListingRequest(
            null, "Tomatoes", 100, "kg", new DateOnly(2026, 10, 1), new GeoLocation { Latitude = -29.85, Longitude = 31.02 }, null));

        Assert.Single(matches.Documents);
        Assert.Equal(new[] { "buyer-phone" }, Assert.Single(push.Sent).Tokens);
    }

    [Fact]
    public async Task EditingADemandToTheCropAFarmerHas_CreatesAMatchAndNotifiesThem()
    {
        var push = new FakePushSender();
        var users = new FakeRepository<UserProfile>(AFarmerWithPhone(), ABuyerWithPhone());
        var demand = ADemand(crop: "Maize");
        var demands = new FakeRepository<DemandRequest>(demand);
        var listings = new FakeRepository<Listing>(AListing());
        var matches = new FakeRepository<MatchDocument>();
        var controller = new DemandsController(demands, users, Matching.Service(listings, demands, matches, users, push))
        {
            ControllerContext = SignedInAs.User(Buyer)
        };

        await controller.UpdateDemand(demand.Id, new DemandsController.CreateDemandRequest(
            null, "Tomatoes", 100, "kg", Future, new GeoLocation { Latitude = -29.85, Longitude = 31.02 }));

        Assert.Single(matches.Documents);
        Assert.Equal(new[] { "farmer-phone" }, Assert.Single(push.Sent).Tokens);
    }

    // ---- Browse privacy ------------------------------------------------------------

    [Fact]
    public void Redact_RoundsCoordinatesToAboutAKilometre()
    {
        var listing = AListing();
        listing.Location = new GeoLocation { Latitude = -29.857149, Longitude = 31.024688 };

        var redacted = BrowsePrivacy.Redact(listing);

        Assert.Equal(-29.86, redacted.Location.Latitude);
        Assert.Equal(31.02, redacted.Location.Longitude);
    }

    [Fact]
    public void Redact_RemovesTheOwnersAccountIdAndTheDeviceGeneratedId()
    {
        var listing = AListing();
        listing.ClientGeneratedId = "device-uuid";
        var demand = ADemand();
        demand.ClientGeneratedId = "device-uuid";

        var redactedListing = BrowsePrivacy.Redact(listing);
        var redactedDemand = BrowsePrivacy.Redact(demand);

        Assert.Equal(string.Empty, redactedListing.FarmerId);
        Assert.Null(redactedListing.ClientGeneratedId);
        Assert.Equal(string.Empty, redactedDemand.BuyerId);
        Assert.Null(redactedDemand.ClientGeneratedId);
    }

    [Fact]
    public void Redact_KeepsWhatABrowserNeeds()
    {
        var listing = AListing();
        listing.PhotoUrl = "data:image/jpeg;base64,AAAA";

        var redacted = BrowsePrivacy.Redact(listing);

        Assert.Equal(listing.Id, redacted.Id);
        Assert.Equal("Tomatoes", redacted.CropType);
        Assert.Equal(100, redacted.Quantity);
        Assert.Equal("data:image/jpeg;base64,AAAA", redacted.PhotoUrl);
        Assert.Equal("Active", redacted.Status);
    }

    [Fact]
    public void Redact_LeavesDistancesAccurateToWithinTwoKilometres()
    {
        var exact = new GeoLocation { Latitude = -29.857149, Longitude = 31.024688 };
        var other = new GeoLocation { Latitude = -29.60, Longitude = 30.90 };
        var listing = AListing();
        listing.Location = exact;

        var before = MatchingService.CalculateDistanceKm(exact.Latitude, exact.Longitude, other.Latitude, other.Longitude);
        var coarse = BrowsePrivacy.Redact(listing).Location;
        var after = MatchingService.CalculateDistanceKm(coarse.Latitude, coarse.Longitude, other.Latitude, other.Longitude);

        Assert.InRange(Math.Abs(before - after), 0, 2);
    }

    // ---- Helpers -------------------------------------------------------------------

    private static MatchesController MatchesFor(
        string uid,
        FakeRepository<MatchDocument> matches,
        Listing listing,
        DemandRequest demand,
        FakeRepository<UserProfile>? users = null,
        FakePushSender? push = null)
    {
        var userRepository = users ?? new FakeRepository<UserProfile>();
        var pushSender = push ?? new FakePushSender();
        var listings = new FakeRepository<Listing>(listing);
        var demands = new FakeRepository<DemandRequest>(demand);
        return new MatchesController(
            userRepository,
            matches,
            Matching.Service(listings, demands, matches, userRepository, pushSender),
            Matching.Notifications(userRepository, pushSender))
        {
            ControllerContext = SignedInAs.User(uid)
        };
    }

    private static List<MatchesController.MatchFeedItem> Feed(ActionResult<List<MatchesController.MatchFeedItem>> result) =>
        Assert.IsType<List<MatchesController.MatchFeedItem>>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static UserProfile AProfile(string uid, string role) =>
        new() { Id = uid, UserId = uid, Role = role, DisplayName = role };

    private static UserProfile AFarmerWithPhone()
    {
        var profile = AProfile(Farmer, "Farmer");
        profile.DeviceTokens.Add(new DeviceToken { Token = "farmer-phone" });
        return profile;
    }

    private static UserProfile ABuyerWithPhone()
    {
        var profile = AProfile(Buyer, "Buyer");
        profile.DeviceTokens.Add(new DeviceToken { Token = "buyer-phone" });
        return profile;
    }

    private static MatchDocument AMatch(string status, string farmer = Farmer, string buyer = Buyer) => new()
    {
        Id = MatchId,
        ListingId = "listing-1",
        DemandRequestId = "demand-1",
        FarmerId = farmer,
        BuyerId = buyer,
        Score = 0.9,
        Status = status
    };

    private static Listing AListing(string status = "Active", string crop = "Tomatoes") => new()
    {
        Id = "listing-1",
        FarmerId = Farmer,
        CropType = crop,
        Quantity = 100,
        Unit = "kg",
        HarvestDate = new DateOnly(2026, 10, 1),
        Location = new GeoLocation { Latitude = -29.85, Longitude = 31.02 },
        Status = status
    };

    private static DemandRequest ADemand(
        string status = "Open", DateOnly? deadline = null, string buyer = Buyer, string crop = "Tomatoes") => new()
    {
        Id = "demand-1",
        BuyerId = buyer,
        CropType = crop,
        QuantityNeeded = 100,
        Unit = "kg",
        Deadline = deadline ?? Future,
        Location = new GeoLocation { Latitude = -29.85, Longitude = 31.02 },
        Status = status
    };
}
