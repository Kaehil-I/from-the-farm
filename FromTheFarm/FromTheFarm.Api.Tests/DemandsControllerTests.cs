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

    private static DemandsController ControllerFor(FakeRepository<DemandRequest> repository, string uid) =>
        new(repository) { ControllerContext = SignedInAs.User(uid) };

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
