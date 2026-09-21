using FromTheFarm.Api.Controllers;
using FromTheFarm.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FromTheFarm.Api.Tests;

public class AuthControllerTests
{
    private const string Uid = "firebase-uid-1";

    [Fact]
    public async Task ExchangeSession_CreatesAProfile_OnFirstSignIn()
    {
        var users = new FakeRepository<UserProfile>();
        var controller = ControllerFor(users, name: "Zola Nkosi");

        var session = Session(await controller.ExchangeSession(new AuthController.SessionRequest("zu")));

        Assert.True(session.IsNewUser);
        Assert.Equal(Uid, session.UserId);
        Assert.Null(session.Role);
        Assert.False(session.OnboardingComplete);
        Assert.Equal("Zola Nkosi", session.Profile.DisplayName);
        Assert.Equal("zu", session.Profile.Language);

        var stored = Assert.Single(users.Documents.Values);
        Assert.Equal(Uid, stored.Id);
        Assert.Equal(Uid, stored.UserId);
    }

    [Fact]
    public async Task ExchangeSession_FallsBackToADefaultName_WhenTheTokenHasNone()
    {
        var controller = ControllerFor(new FakeRepository<UserProfile>(), name: null);

        var session = Session(await controller.ExchangeSession(new AuthController.SessionRequest("en")));

        Assert.Equal("New user", session.Profile.DisplayName);
    }

    [Fact]
    public async Task ExchangeSession_DefaultsTheLanguageToEnglish_WhenTheDeviceDoesNotSendOne()
    {
        var controller = ControllerFor(new FakeRepository<UserProfile>());

        var session = Session(await controller.ExchangeSession(new AuthController.SessionRequest(null)));

        Assert.Equal("en", session.Profile.Language);
    }

    [Fact]
    public async Task ExchangeSession_ReturnsTheExistingProfile_WithoutOverwritingIt()
    {
        var existing = new UserProfile
        {
            Id = Uid,
            UserId = Uid,
            DisplayName = "Original Name",
            Role = "Farmer",
            Language = "af",
            SearchRadiusKm = 25
        };
        var users = new FakeRepository<UserProfile>(existing);
        var controller = ControllerFor(users, name: "A Different Name");

        var session = Session(await controller.ExchangeSession(new AuthController.SessionRequest("zu")));

        Assert.False(session.IsNewUser);
        Assert.Equal("Farmer", session.Role);
        Assert.True(session.OnboardingComplete);
        Assert.Equal("Original Name", session.Profile.DisplayName);
        Assert.Equal("af", session.Profile.Language);
        Assert.Equal(25, session.Profile.SearchRadiusKm);
        Assert.Equal(0, users.WriteCount);
    }

    private static AuthController ControllerFor(FakeRepository<UserProfile> users, string? name = null) =>
        new(users) { ControllerContext = SignedInAs.User(Uid, name) };

    private static AuthController.SessionResponse Session(ActionResult<AuthController.SessionResponse> result) =>
        Assert.IsType<AuthController.SessionResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
