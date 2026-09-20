using FromTheFarm.Api.Controllers;
using FromTheFarm.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FromTheFarm.Api.Tests;

public class UsersControllerTests
{
    private const string Uid = "user-1";

    [Theory]
    [InlineData("Landowner")]
    [InlineData("farmer")]
    [InlineData("")]
    public async Task UpdateMyProfile_RejectsARoleOutsideTheAllowedSet(string role)
    {
        var repository = new FakeRepository<UserProfile>(ExistingProfile());
        var controller = ControllerFor(repository);

        var result = await controller.UpdateMyProfile(Request(role: role));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, repository.WriteCount);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("EN")]
    [InlineData("")]
    public async Task UpdateMyProfile_RejectsAnUnsupportedLanguage(string language)
    {
        var repository = new FakeRepository<UserProfile>(ExistingProfile());
        var controller = ControllerFor(repository);

        var result = await controller.UpdateMyProfile(Request(language: language));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, repository.WriteCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-5)]
    public async Task UpdateMyProfile_RejectsARadiusOutsideTheSupportedRange(int radius)
    {
        var repository = new FakeRepository<UserProfile>(ExistingProfile());
        var controller = ControllerFor(repository);

        var result = await controller.UpdateMyProfile(Request(searchRadiusKm: radius));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, repository.WriteCount);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("not a phone number")]
    [InlineData("+27 82 555 0143 extension 12345678901234")]
    public async Task UpdateMyProfile_RejectsAMalformedPhoneNumber(string phone)
    {
        var repository = new FakeRepository<UserProfile>(ExistingProfile());
        var controller = ControllerFor(repository);

        var result = await controller.UpdateMyProfile(Request(phone: phone));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task UpdateMyProfile_PersistsTheEditedValues()
    {
        var repository = new FakeRepository<UserProfile>(ExistingProfile());
        var controller = ControllerFor(repository);

        var result = await controller.UpdateMyProfile(
            Request(role: "Buyer", language: "zu", searchRadiusKm: 42, phone: "+27 82 555 0143"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<UserProfile>(ok.Value);
        Assert.Equal("Buyer", profile.Role);
        Assert.Equal("zu", profile.Language);
        Assert.Equal(42, profile.SearchRadiusKm);
        Assert.Equal("+27 82 555 0143", profile.Phone);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateMyProfile_StoresAClearedPhoneNumberAsAbsent(string? phone)
    {
        var repository = new FakeRepository<UserProfile>(ExistingProfile(phone: "+27 82 555 0143"));
        var controller = ControllerFor(repository);

        await controller.UpdateMyProfile(Request(phone: phone));

        Assert.Null(repository.Documents[Uid].Phone);
    }

    [Fact]
    public async Task UpdateMyProfile_ReturnsNotFound_WhenNoSessionHasBeenCreated()
    {
        var repository = new FakeRepository<UserProfile>();
        var controller = ControllerFor(repository);

        var result = await controller.UpdateMyProfile(Request());

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetMyProfile_ReturnsOnlyTheSignedInUsersProfile()
    {
        var repository = new FakeRepository<UserProfile>(
            ExistingProfile(),
            ExistingProfile(id: "someone-else"));
        var controller = ControllerFor(repository);

        var result = await controller.GetMyProfile();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<UserProfile>(ok.Value);
        Assert.Equal(Uid, profile.Id);
    }

    private static UserProfile ExistingProfile(string id = Uid, string? phone = null) => new()
    {
        Id = id,
        UserId = id,
        DisplayName = "Test User",
        Role = "Farmer",
        Language = "en",
        SearchRadiusKm = 10,
        Phone = phone
    };

    private static UsersController ControllerFor(FakeRepository<UserProfile> repository) =>
        new(repository) { ControllerContext = SignedInAs.User(Uid) };

    private static UsersController.UpdateProfileRequest Request(
        string role = "Farmer",
        string language = "en",
        int searchRadiusKm = 10,
        bool notificationsEnabled = true,
        bool biometricLockEnabled = false,
        string? phone = null) =>
        new(role, language, searchRadiusKm, notificationsEnabled, biometricLockEnabled, phone);
}
