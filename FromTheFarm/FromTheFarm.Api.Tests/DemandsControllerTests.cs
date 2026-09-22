using FromTheFarm.Api.Controllers;
using FromTheFarm.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FromTheFarm.Api.Tests;

public class DemandsControllerTests
{
    private const string Owner = "buyer-1";
    private const string Stranger = "buyer-2";

    [Fact]
    public async Task UpdateDemand_IsRefused_WhenTheCallerIsNotTheOwner()
    {
        var repository = new FakeRepository<DemandRequest>(ExistingDemand());
        var controller = ControllerFor(repository, Stranger);

        var result = await controller.UpdateDemand("demand-1", Request());

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Equal(0, repository.WriteCount);
        Assert.Equal("Tomatoes", repository.Documents["demand-1"].CropType);
    }

    [Fact]
    public async Task DeleteDemand_IsRefused_WhenTheCallerIsNotTheOwner()
    {
        var repository = new FakeRepository<DemandRequest>(ExistingDemand());
        var controller = ControllerFor(repository, Stranger);

        var result = await controller.DeleteDemand("demand-1");

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, repository.WriteCount);
        Assert.Equal("Open", repository.Documents["demand-1"].Status);
    }

    [Fact]
    public async Task DeleteDemand_ExpiresTheRecord_ForTheOwner()
    {
        var repository = new FakeRepository<DemandRequest>(ExistingDemand());
        var controller = ControllerFor(repository, Owner);

        var result = await controller.DeleteDemand("demand-1");

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("Expired", repository.Documents["demand-1"].Status);
    }

    [Fact]
    public async Task UpdateDemand_AppliesTheEdit_ForTheOwner()
    {
        var repository = new FakeRepository<DemandRequest>(ExistingDemand());
        var controller = ControllerFor(repository, Owner);

        var result = await controller.UpdateDemand("demand-1", Request(cropType: "Spinach", quantityNeeded: 8));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var demand = Assert.IsType<DemandRequest>(ok.Value);
        Assert.Equal("Spinach", demand.CropType);
        Assert.Equal(8m, demand.QuantityNeeded);
    }

    [Fact]
    public async Task CreateDemand_RejectsADeadlineInThePast()
    {
        var repository = new FakeRepository<DemandRequest>();
        var controller = ControllerFor(repository, Owner);

        var result = await controller.CreateDemand(Request(deadline: new DateOnly(2020, 1, 1)));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task CreateDemand_AcceptsTodayAsADeadline()
    {
        var repository = new FakeRepository<DemandRequest>();
        var controller = ControllerFor(repository, Owner);

        var result = await controller.CreateDemand(
            Request(deadline: DateOnly.FromDateTime(DateTime.UtcNow)));

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(1, repository.WriteCount);
    }

    [Fact]
    public async Task CreateDemand_RejectsAnOutOfRangeLongitude()
    {
        var repository = new FakeRepository<DemandRequest>();
        var controller = ControllerFor(repository, Owner);

        var result = await controller.CreateDemand(Request(longitude: 200));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task UpdateDemand_ReturnsNotFound_WhenTheDemandDoesNotExist()
    {
        var repository = new FakeRepository<DemandRequest>();
        var controller = ControllerFor(repository, Owner);

        var result = await controller.UpdateDemand("demand-1", Request());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeleteDemand_ReturnsNotFound_WhenTheDemandDoesNotExist()
    {
        var repository = new FakeRepository<DemandRequest>();
        var controller = ControllerFor(repository, Owner);

        var result = await controller.DeleteDemand("demand-1");

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task DeleteDemand_ReturnsNotFound_NotForbidden_ForAnyCallerWhenTheDemandDoesNotExist()
    {
        // Existence is checked before ownership, so a missing record is a plain
        // 404 for everyone rather than a 403 that would imply it exists.
        var repository = new FakeRepository<DemandRequest>();
        var controller = ControllerFor(repository, Stranger);

        var result = await controller.DeleteDemand("demand-1");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateDemand_LeavesTheOwnerAndIdUnchanged()
    {
        var repository = new FakeRepository<DemandRequest>(ExistingDemand());
        var controller = ControllerFor(repository, Owner);

        var result = await controller.UpdateDemand("demand-1", Request(cropType: "Spinach"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<DemandRequest>(ok.Value);
        Assert.Equal(Owner, returned.BuyerId);
        Assert.Equal("demand-1", returned.Id);
        Assert.Equal(Owner, repository.Documents["demand-1"].BuyerId);
    }

    private static DemandRequest ExistingDemand() => new()
    {
        Id = "demand-1",
        BuyerId = Owner,
        CropType = "Tomatoes",
        QuantityNeeded = 20,
        Unit = "kg",
        Deadline = new DateOnly(2030, 1, 1),
        Location = new GeoLocation { Latitude = -29.85, Longitude = 31.02 },
        Status = "Open"
    };

    [Theory]
    [InlineData("Farmer")]
    [InlineData(null)]
    public async Task CreateDemand_IsRefused_ForACallerWhoIsNotABuyer(string? role)
    {
        var repository = new FakeRepository<DemandRequest>();
        var controller = ControllerFor(repository, Owner, Profile(Owner, role));

        var result = await controller.CreateDemand(Request());

        var refused = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, refused.StatusCode);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task CreateDemand_IsRefused_WhenTheCallerHasNoProfileAtAll()
    {
        var repository = new FakeRepository<DemandRequest>();
        var controller = new DemandsController(repository, new FakeRepository<UserProfile>())
        {
            ControllerContext = SignedInAs.User(Owner)
        };

        var result = await controller.CreateDemand(Request());

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task CreateDemand_ChecksTheRoleBeforeValidatingTheRequest()
    {
        var repository = new FakeRepository<DemandRequest>();
        var controller = ControllerFor(repository, Owner, Profile(Owner, "Farmer"));

        var result = await controller.CreateDemand(Request(deadline: new DateOnly(2020, 1, 1)));

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task CreateDemand_IsAllowed_ForABuyer()
    {
        var repository = new FakeRepository<DemandRequest>();
        var controller = ControllerFor(repository, Owner);

        var result = await controller.CreateDemand(Request());

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(1, repository.WriteCount);
    }

    private static UserProfile Profile(string uid, string? role) =>
        new() { Id = uid, UserId = uid, Role = role };

    // Callers are buyers unless a test says otherwise, so the existing ownership
    // and validation tests exercise the paths they were written for.
    private static DemandsController ControllerFor(
        FakeRepository<DemandRequest> repository, string uid, UserProfile? profile = null) =>
        new(repository, new FakeRepository<UserProfile>(profile ?? Profile(uid, "Buyer")))
        {
            ControllerContext = SignedInAs.User(uid)
        };

    private static DemandsController.CreateDemandRequest Request(
        string cropType = "Tomatoes",
        decimal quantityNeeded = 20,
        string unit = "kg",
        double latitude = -29.85,
        double longitude = 31.02,
        DateOnly? deadline = null) =>
        new(
            null,
            cropType,
            quantityNeeded,
            unit,
            deadline ?? new DateOnly(2030, 1, 1),
            new GeoLocation { Latitude = latitude, Longitude = longitude });
}
