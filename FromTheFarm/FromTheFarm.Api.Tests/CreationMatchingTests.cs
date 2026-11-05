using FromTheFarm.Api.Controllers;
using FromTheFarm.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FromTheFarm.Api.Tests;

// Matching runs when a listing or demand is posted, so the other side is
// alerted at once instead of whenever somebody next opens their feed.
public class CreationMatchingTests
{
    private const string Farmer = "farmer-1";
    private const string Buyer = "buyer-1";

    [Fact]
    public async Task PostingAListing_CreatesAMatchAndNotifiesTheBuyerWithAnOpenDemand()
    {
        var push = new FakePushSender();
        var matches = new FakeRepository<MatchDocument>();
        var users = new FakeRepository<UserProfile>(AFarmer(), ABuyer());
        var demands = new FakeRepository<DemandRequest>(ADemand());
        var listings = new FakeRepository<Listing>();
        var controller = new ListingsController(listings, users, Matching.Service(listings, demands, matches, users, push))
        {
            ControllerContext = SignedInAs.User(Farmer)
        };

        var result = await controller.CreateListing(AListingRequest());

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var match = Assert.Single(matches.Documents.Values);
        Assert.Equal(Farmer, match.FarmerId);
        Assert.Equal(Buyer, match.BuyerId);
        var (tokens, message) = Assert.Single(push.Sent);
        Assert.Equal(new[] { "buyer-phone" }, tokens);
        Assert.Equal("match_new", message.Data["type"]);
        Assert.Equal(match.Id, message.Data["matchId"]);
    }

    [Fact]
    public async Task PostingADemand_CreatesAMatchAndNotifiesTheFarmerWithAnActiveListing()
    {
        var push = new FakePushSender();
        var matches = new FakeRepository<MatchDocument>();
        var users = new FakeRepository<UserProfile>(AFarmer(), ABuyer());
        var listings = new FakeRepository<Listing>(AListing());
        var demands = new FakeRepository<DemandRequest>();
        var controller = new DemandsController(demands, users, Matching.Service(listings, demands, matches, users, push))
        {
            ControllerContext = SignedInAs.User(Buyer)
        };

        var result = await controller.CreateDemand(ADemandRequest());

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Single(matches.Documents.Values);
        var (tokens, _) = Assert.Single(push.Sent);
        Assert.Equal(new[] { "farmer-phone" }, tokens);
    }

    [Fact]
    public async Task PostingAListing_CreatesNoMatch_WhenNoDemandWantsThatCrop()
    {
        var push = new FakePushSender();
        var matches = new FakeRepository<MatchDocument>();
        var users = new FakeRepository<UserProfile>(AFarmer(), ABuyer());
        var demands = new FakeRepository<DemandRequest>(ADemand(crop: "Maize"));
        var listings = new FakeRepository<Listing>();
        var controller = new ListingsController(listings, users, Matching.Service(listings, demands, matches, users, push))
        {
            ControllerContext = SignedInAs.User(Farmer)
        };

        await controller.CreateListing(AListingRequest());

        Assert.Empty(matches.Documents);
        Assert.Empty(push.Sent);
    }

    [Fact]
    public async Task PostingAListing_StillSucceeds_WhenMatchingFails()
    {
        var users = new FakeRepository<UserProfile>(AFarmer());
        var listings = new FakeRepository<Listing>();
        var failing = new FailingDemands();
        var matching = new FromTheFarm.Api.Services.MatchGenerationService(
            listings, failing, new FakeRepository<MatchDocument>(), new FromTheFarm.Api.Services.MatchingService(),
            Matching.Notifications(users),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FromTheFarm.Api.Services.MatchGenerationService>.Instance);
        var controller = new ListingsController(listings, users, matching) { ControllerContext = SignedInAs.User(Farmer) };

        var result = await controller.CreateListing(AListingRequest());

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Single(listings.Documents);
    }

    [Fact]
    public async Task ARejectedListing_DoesNotTriggerMatching()
    {
        var push = new FakePushSender();
        var users = new FakeRepository<UserProfile>(AFarmer(), ABuyer());
        var demands = new FakeRepository<DemandRequest>(ADemand());
        var listings = new FakeRepository<Listing>();
        var matches = new FakeRepository<MatchDocument>();
        var controller = new ListingsController(listings, users, Matching.Service(listings, demands, matches, users, push))
        {
            ControllerContext = SignedInAs.User(Farmer)
        };

        var result = await controller.CreateListing(AListingRequest(quantity: 0));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(matches.Documents);
        Assert.Empty(push.Sent);
    }

    private sealed class FailingDemands : FakeRepository<DemandRequest>
    {
        public override Task<List<DemandRequest>> FindAsync(System.Linq.Expressions.Expression<Func<DemandRequest, bool>> filter) =>
            throw new InvalidOperationException("Database unreachable.");
    }

    private static UserProfile AFarmer() => WithDevice(
        new UserProfile { Id = Farmer, UserId = Farmer, Role = "Farmer", DisplayName = "Fay" }, "farmer-phone");

    private static UserProfile ABuyer() => WithDevice(
        new UserProfile { Id = Buyer, UserId = Buyer, Role = "Buyer", DisplayName = "Ben" }, "buyer-phone");

    private static UserProfile WithDevice(UserProfile profile, string token)
    {
        profile.DeviceTokens.Add(new DeviceToken { Token = token });
        return profile;
    }

    private static Listing AListing() => new()
    {
        Id = "listing-1",
        FarmerId = Farmer,
        CropType = "Tomatoes",
        Quantity = 100,
        Unit = "kg",
        HarvestDate = new DateOnly(2026, 10, 1),
        Location = new GeoLocation { Latitude = -29.85, Longitude = 31.02 }
    };

    private static DemandRequest ADemand(string crop = "Tomatoes") => new()
    {
        Id = "demand-1",
        BuyerId = Buyer,
        CropType = crop,
        QuantityNeeded = 100,
        Unit = "kg",
        Deadline = new DateOnly(2030, 1, 1),
        Location = new GeoLocation { Latitude = -29.85, Longitude = 31.02 }
    };

    private static ListingsController.CreateListingRequest AListingRequest(decimal quantity = 50) =>
        new(null, "Tomatoes", quantity, "kg", new DateOnly(2026, 10, 1),
            new GeoLocation { Latitude = -29.85, Longitude = 31.02 }, null);

    private static DemandsController.CreateDemandRequest ADemandRequest() =>
        new(null, "Tomatoes", 20, "kg", new DateOnly(2030, 1, 1),
            new GeoLocation { Latitude = -29.85, Longitude = 31.02 });
}
