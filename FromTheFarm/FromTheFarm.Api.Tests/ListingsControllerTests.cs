using FromTheFarm.Api.Controllers;
using FromTheFarm.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FromTheFarm.Api.Tests;

public class ListingsControllerTests
{
    private const string Owner = "farmer-1";
    private const string Stranger = "farmer-2";

    // Smallest byte sequences that carry a recognisable JPEG / PNG signature.
    private static readonly string JpegBase64 =
        Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 });

    private static readonly string NotAnImageBase64 =
        Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46 });

    [Fact]
    public async Task UpdateListing_IsRefused_WhenTheCallerIsNotTheOwner()
    {
        var repository = new FakeRepository<Listing>(ExistingListing());
        var controller = ControllerFor(repository, Stranger);

        var result = await controller.UpdateListing("listing-1", Request());

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Equal(0, repository.WriteCount);
        Assert.Equal("Tomatoes", repository.Documents["listing-1"].CropType);
    }

    [Fact]
    public async Task DeleteListing_IsRefused_WhenTheCallerIsNotTheOwner()
    {
        var repository = new FakeRepository<Listing>(ExistingListing());
        var controller = ControllerFor(repository, Stranger);

        var result = await controller.DeleteListing("listing-1");

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, repository.WriteCount);
        Assert.Equal("Active", repository.Documents["listing-1"].Status);
    }

    [Fact]
    public async Task DeleteListing_SoftDeletes_ForTheOwner()
    {
        var repository = new FakeRepository<Listing>(ExistingListing());
        var controller = ControllerFor(repository, Owner);

        var result = await controller.DeleteListing("listing-1");

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("Deleted", repository.Documents["listing-1"].Status);
    }

    [Fact]
    public async Task UpdateListing_AppliesTheEdit_ForTheOwner()
    {
        var repository = new FakeRepository<Listing>(ExistingListing());
        var controller = ControllerFor(repository, Owner);

        var result = await controller.UpdateListing("listing-1", Request(cropType: "Spinach", quantity: 12));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var listing = Assert.IsType<Listing>(ok.Value);
        Assert.Equal("Spinach", listing.CropType);
        Assert.Equal(12m, listing.Quantity);
    }

    [Fact]
    public async Task UpdateListing_ReplacesThePhoto_WhenANewOneIsSupplied()
    {
        var repository = new FakeRepository<Listing>(ExistingListing());
        var controller = ControllerFor(repository, Owner);

        await controller.UpdateListing("listing-1", Request(photoBase64: JpegBase64));

        Assert.Equal($"data:image/jpeg;base64,{JpegBase64}", repository.Documents["listing-1"].PhotoUrl);
    }

    [Fact]
    public async Task UpdateListing_KeepsTheExistingPhoto_WhenNoneIsSupplied()
    {
        var repository = new FakeRepository<Listing>(ExistingListing());
        var controller = ControllerFor(repository, Owner);

        await controller.UpdateListing("listing-1", Request(photoBase64: null));

        Assert.Equal("data:image/jpeg;base64,PREVIOUS", repository.Documents["listing-1"].PhotoUrl);
    }

    [Fact]
    public async Task UpdateListing_ReturnsNotFound_WhenTheListingDoesNotExist()
    {
        var repository = new FakeRepository<Listing>();
        var controller = ControllerFor(repository, Owner);

        var result = await controller.UpdateListing("listing-1", Request());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateListing_StoresThePhoto_AsADataUri()
    {
        var repository = new FakeRepository<Listing>();
        var controller = ControllerFor(repository, Owner);

        var result = await controller.CreateListing(Request(photoBase64: JpegBase64));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var listing = Assert.IsType<Listing>(created.Value);
        Assert.Equal($"data:image/jpeg;base64,{JpegBase64}", listing.PhotoUrl);
        Assert.Equal(Owner, listing.FarmerId);
    }

    [Fact]
    public async Task CreateListing_RejectsAPhotoThatIsNotAnImage()
    {
        var repository = new FakeRepository<Listing>();
        var controller = ControllerFor(repository, Owner);

        var result = await controller.CreateListing(Request(photoBase64: NotAnImageBase64));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, repository.WriteCount);
    }

    [Theory]
    [InlineData(91, 31.0)]
    [InlineData(-91, 31.0)]
    [InlineData(-29.85, 181.0)]
    [InlineData(-29.85, -181.0)]
    public async Task CreateListing_RejectsCoordinatesOutsideTheValidRange(double latitude, double longitude)
    {
        var repository = new FakeRepository<Listing>();
        var controller = ControllerFor(repository, Owner);

        var result = await controller.CreateListing(Request(latitude: latitude, longitude: longitude));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task CreateListing_RejectsANonPositiveQuantity()
    {
        var repository = new FakeRepository<Listing>();
        var controller = ControllerFor(repository, Owner);

        var result = await controller.CreateListing(Request(quantity: 0));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task CreateListing_RejectsABlankUnit()
    {
        var repository = new FakeRepository<Listing>();
        var controller = ControllerFor(repository, Owner);

        var result = await controller.CreateListing(Request(unit: "   "));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, repository.WriteCount);
    }

    private static Listing ExistingListing() => new()
    {
        Id = "listing-1",
        FarmerId = Owner,
        CropType = "Tomatoes",
        Quantity = 50,
        Unit = "kg",
        HarvestDate = new DateOnly(2026, 10, 1),
        Location = new GeoLocation { Latitude = -29.85, Longitude = 31.02 },
        PhotoUrl = "data:image/jpeg;base64,PREVIOUS",
        Status = "Active"
    };

    private static ListingsController ControllerFor(FakeRepository<Listing> repository, string uid) =>
        new(repository) { ControllerContext = SignedInAs.User(uid) };

    private static ListingsController.CreateListingRequest Request(
        string cropType = "Tomatoes",
        decimal quantity = 50,
        string unit = "kg",
        double latitude = -29.85,
        double longitude = 31.02,
        string? photoBase64 = null) =>
        new(
            null,
            cropType,
            quantity,
            unit,
            new DateOnly(2026, 10, 1),
            new GeoLocation { Latitude = latitude, Longitude = longitude },
            photoBase64);
}
