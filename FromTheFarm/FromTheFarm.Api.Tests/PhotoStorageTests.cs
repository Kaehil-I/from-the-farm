using FromTheFarm.Api.Controllers;
using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FromTheFarm.Api.Tests;

// Listing photos now go to blob storage and the listing keeps only a link.
public class PhotoStorageTests
{
    private const string Owner = "farmer-1";
    private const string Host = "from-the-farm.onrender.com";

    // Smallest byte sequence that carries a recognisable JPEG signature.
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
    private static readonly string JpegBase64 = Convert.ToBase64String(JpegBytes);

    // ---- Create --------------------------------------------------------------------

    [Fact]
    public async Task CreateListing_SendsThePhotoToBlobStorage_AndKeepsOnlyALink()
    {
        var store = new FakePhotoStore();
        var controller = ControllerFor(new FakeRepository<Listing>(), store);

        var result = await controller.CreateListing(Request(JpegBase64));

        var listing = Assert.IsType<Listing>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        var id = Assert.Single(store.Blobs.Keys);
        Assert.Equal($"https://{Host}/api/v1/photos/{id}", listing.PhotoUrl);
        Assert.DoesNotContain("base64", listing.PhotoUrl);
        Assert.Equal(JpegBytes, store.Blobs[id].Bytes);
        Assert.Equal("image/jpeg", store.Blobs[id].ContentType);
    }

    [Fact]
    public async Task CreateListing_WithoutAPhoto_StoresNothing()
    {
        var store = new FakePhotoStore();
        var controller = ControllerFor(new FakeRepository<Listing>(), store);

        var result = await controller.CreateListing(Request(null));

        var listing = Assert.IsType<Listing>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Null(listing.PhotoUrl);
        Assert.Empty(store.Blobs);
    }

    [Fact]
    public async Task CreateListing_RefusesAnInvalidPhoto_WithoutStoringAnything()
    {
        var store = new FakePhotoStore();
        var controller = ControllerFor(new FakeRepository<Listing>(), store);

        var result = await controller.CreateListing(Request(Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46 })));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(store.Blobs);
    }

    // ---- Update and delete ---------------------------------------------------------

    [Fact]
    public async Task UpdateListing_ReplacesThePhoto_AndDeletesTheOldBlob()
    {
        var store = new FakePhotoStore();
        var oldId = await store.SaveAsync(new byte[] { 1 }, "image/jpeg");
        var repository = new FakeRepository<Listing>(Existing(PhotoLinks.Route + oldId));
        var controller = ControllerFor(repository, store);

        await controller.UpdateListing("listing-1", Request(JpegBase64));

        Assert.DoesNotContain(oldId, store.Blobs.Keys);
        var newId = Assert.Single(store.Blobs.Keys);
        Assert.EndsWith(newId, repository.Documents["listing-1"].PhotoUrl);
    }

    [Fact]
    public async Task UpdateListing_KeepsThePhoto_WhenNoneIsSupplied()
    {
        var store = new FakePhotoStore();
        var id = await store.SaveAsync(new byte[] { 1 }, "image/jpeg");
        var url = PhotoLinks.Route + id;
        var repository = new FakeRepository<Listing>(Existing(url));
        var controller = ControllerFor(repository, store);

        await controller.UpdateListing("listing-1", Request(null));

        Assert.Equal(url, repository.Documents["listing-1"].PhotoUrl);
        Assert.Contains(id, store.Blobs.Keys);
    }

    [Fact]
    public async Task UpdateListing_MovesALegacyInlinePhotoToBlobStorage_WithoutAnythingToDelete()
    {
        var store = new FakePhotoStore();
        var repository = new FakeRepository<Listing>(Existing("data:image/jpeg;base64,PREVIOUS"));
        var controller = ControllerFor(repository, store);

        await controller.UpdateListing("listing-1", Request(JpegBase64));

        Assert.Empty(store.Deleted);
        Assert.StartsWith($"https://{Host}/api/v1/photos/", repository.Documents["listing-1"].PhotoUrl);
    }

    [Fact]
    public async Task DeleteListing_FreesTheStoredPhoto()
    {
        var store = new FakePhotoStore();
        var id = await store.SaveAsync(new byte[] { 1 }, "image/jpeg");
        var repository = new FakeRepository<Listing>(Existing(PhotoLinks.Route + id));
        var controller = ControllerFor(repository, store);

        var result = await controller.DeleteListing("listing-1");

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(store.Blobs);
        Assert.Null(repository.Documents["listing-1"].PhotoUrl);
        Assert.Equal("Deleted", repository.Documents["listing-1"].Status);
    }

    // ---- Serving -------------------------------------------------------------------

    [Fact]
    public async Task GetPhoto_ReturnsTheStoredBytes_WithItsTypeAndALongCache()
    {
        var store = new FakePhotoStore();
        var id = await store.SaveAsync(JpegBytes, "image/jpeg");
        var controller = PhotosFor(store);

        var result = await controller.GetPhoto(id);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(JpegBytes, file.FileContents);
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.Contains("immutable", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"].ToString());
    }

    [Fact]
    public async Task GetPhoto_IsNotFound_ForAnUnknownId()
    {
        var result = await PhotosFor(new FakePhotoStore()).GetPhoto(new string('a', 32));

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("not-a-photo-id")]
    [InlineData("..%2f..%2fsecrets")]
    public async Task GetPhoto_IsNotFound_ForAnIdOfTheWrongShape_WithoutTouchingStorage(string id)
    {
        var store = new FakePhotoStore();

        var result = await PhotosFor(store).GetPhoto(id);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, store.OpenCalls);
    }

    // ---- Links ---------------------------------------------------------------------

    [Fact]
    public void PhotoLinks_ForcesHttps_ForARealHost_EvenBehindATlsProxy()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "http";
        request.Host = new HostString(Host);

        Assert.Equal($"https://{Host}/api/v1/photos/abc", PhotoLinks.For(request, "abc"));
    }

    [Fact]
    public void PhotoLinks_KeepsHttp_ForLocalDevelopment()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "http";
        request.Host = new HostString("localhost", 5000);

        Assert.Equal("http://localhost:5000/api/v1/photos/abc", PhotoLinks.For(request, "abc"));
    }

    [Fact]
    public void PhotoLinks_IdFrom_ReadsOurLinks_AndIgnoresEverythingElse()
    {
        var id = new string('f', 32);

        Assert.Equal(id, PhotoLinks.IdFrom($"https://{Host}/api/v1/photos/{id}"));
        Assert.Null(PhotoLinks.IdFrom("data:image/jpeg;base64,AAAA"));
        Assert.Null(PhotoLinks.IdFrom("https://elsewhere.example/other.jpg"));
        Assert.Null(PhotoLinks.IdFrom(null));
    }

    // ---- helpers -------------------------------------------------------------------

    private static ListingsController ControllerFor(FakeRepository<Listing> listings, FakePhotoStore store)
    {
        var users = new FakeRepository<UserProfile>(new UserProfile { Id = Owner, UserId = Owner, Role = "Farmer" });
        var context = SignedInAs.User(Owner);
        context.HttpContext.Request.Scheme = "http";
        context.HttpContext.Request.Host = new HostString(Host);
        return new ListingsController(listings, users, Matching.Service(listings: listings), store)
        {
            ControllerContext = context
        };
    }

    private static PhotosController PhotosFor(FakePhotoStore store) =>
        new(store) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static Listing Existing(string? photoUrl) => new()
    {
        Id = "listing-1",
        FarmerId = Owner,
        CropType = "Tomatoes",
        Quantity = 50,
        Unit = "kg",
        HarvestDate = new DateOnly(2030, 1, 1),
        Location = new GeoLocation { Latitude = -29.85, Longitude = 31.02 },
        Status = "Active",
        PhotoUrl = photoUrl
    };

    private static ListingsController.CreateListingRequest Request(string? photoBase64) =>
        new(null, "Tomatoes", 50, "kg", new DateOnly(2030, 1, 1),
            new GeoLocation { Latitude = -29.85, Longitude = 31.02 }, photoBase64);
}

// Holds blobs in memory and records what was deleted.
internal sealed class FakePhotoStore : IPhotoStore
{
    public Dictionary<string, StoredPhoto> Blobs { get; } = new();
    public List<string> Deleted { get; } = new();
    public int OpenCalls { get; private set; }

    public Task<string> SaveAsync(byte[] bytes, string contentType)
    {
        var id = Guid.NewGuid().ToString("N");
        Blobs[id] = new StoredPhoto(bytes, contentType);
        return Task.FromResult(id);
    }

    public Task<StoredPhoto?> OpenAsync(string id)
    {
        OpenCalls++;
        return Task.FromResult(Blobs.TryGetValue(id, out var photo) ? photo : null);
    }

    public Task DeleteAsync(string id)
    {
        Deleted.Add(id);
        Blobs.Remove(id);
        return Task.CompletedTask;
    }
}
