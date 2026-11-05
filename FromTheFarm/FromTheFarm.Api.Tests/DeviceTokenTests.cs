using FromTheFarm.Api.Controllers;
using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FromTheFarm.Api.Tests;

public class DeviceTokenTests
{
    private const string Uid = "user-1";
    private const string Token = "fcm-token-aaaaaaaaaaaaaaaa:APA91bExample";

    [Fact]
    public async Task RegisterDevice_StoresTheTokenOnTheCallersProfile()
    {
        var users = new FakeRepository<UserProfile>(Profile(Uid));

        var result = await ControllerFor(users).RegisterDevice(new UsersController.DeviceTokenRequest(Token));

        Assert.IsType<NoContentResult>(result);
        var stored = Assert.Single(users.Documents[Uid].DeviceTokens);
        Assert.Equal(Token, stored.Token);
    }

    [Fact]
    public async Task RegisterDevice_RefreshesAnExistingTokenInsteadOfDuplicatingIt()
    {
        var profile = Profile(Uid);
        profile.DeviceTokens.Add(new DeviceToken
        {
            Token = Token,
            RegisteredAt = DateTime.UtcNow.AddDays(-30),
            LastSeenAt = DateTime.UtcNow.AddDays(-30)
        });
        var users = new FakeRepository<UserProfile>(profile);

        await ControllerFor(users).RegisterDevice(new UsersController.DeviceTokenRequest(Token));

        var stored = Assert.Single(users.Documents[Uid].DeviceTokens);
        Assert.True(stored.LastSeenAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.True(stored.RegisteredAt < DateTime.UtcNow.AddDays(-29));
    }

    [Fact]
    public async Task RegisterDevice_MovesTheTokenOffAnotherAccountThatUsedTheSamePhone()
    {
        var previous = Profile("user-2");
        previous.DeviceTokens.Add(new DeviceToken { Token = Token });
        var users = new FakeRepository<UserProfile>(Profile(Uid), previous);

        await ControllerFor(users).RegisterDevice(new UsersController.DeviceTokenRequest(Token));

        Assert.Empty(users.Documents["user-2"].DeviceTokens);
        Assert.Single(users.Documents[Uid].DeviceTokens);
    }

    [Fact]
    public async Task RegisterDevice_KeepsTheOtherAccountsDifferentTokens()
    {
        var other = Profile("user-2");
        other.DeviceTokens.Add(new DeviceToken { Token = "someone-elses-phone" });
        var users = new FakeRepository<UserProfile>(Profile(Uid), other);

        await ControllerFor(users).RegisterDevice(new UsersController.DeviceTokenRequest(Token));

        Assert.Single(users.Documents["user-2"].DeviceTokens);
    }

    [Fact]
    public async Task RegisterDevice_EvictsTheLeastRecentlySeenTokenAtTheCap()
    {
        var profile = Profile(Uid);
        for (var i = 0; i < RequestValidation.MaxDevicesPerUser; i++)
        {
            profile.DeviceTokens.Add(new DeviceToken
            {
                Token = $"device-{i}",
                LastSeenAt = DateTime.UtcNow.AddDays(-(i + 1))
            });
        }
        var users = new FakeRepository<UserProfile>(profile);

        await ControllerFor(users).RegisterDevice(new UsersController.DeviceTokenRequest(Token));

        var tokens = users.Documents[Uid].DeviceTokens.Select(t => t.Token).ToList();
        Assert.Equal(RequestValidation.MaxDevicesPerUser, tokens.Count);
        Assert.Contains(Token, tokens);
        Assert.DoesNotContain($"device-{RequestValidation.MaxDevicesPerUser - 1}", tokens);
        Assert.Contains("device-0", tokens);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has a space")]
    [InlineData("new\nline")]
    public async Task RegisterDevice_RejectsAnInvalidToken(string token)
    {
        var users = new FakeRepository<UserProfile>(Profile(Uid));

        var result = await ControllerFor(users).RegisterDevice(new UsersController.DeviceTokenRequest(token));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, users.WriteCount);
    }

    [Fact]
    public async Task RegisterDevice_RejectsAnOversizedToken()
    {
        var users = new FakeRepository<UserProfile>(Profile(Uid));
        var token = new string('a', RequestValidation.MaxDeviceTokenLength + 1);

        var result = await ControllerFor(users).RegisterDevice(new UsersController.DeviceTokenRequest(token));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, users.WriteCount);
    }

    [Fact]
    public async Task RegisterDevice_ReturnsNotFound_BeforeASessionExists()
    {
        var users = new FakeRepository<UserProfile>();

        var result = await ControllerFor(users).RegisterDevice(new UsersController.DeviceTokenRequest(Token));

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(0, users.WriteCount);
    }

    [Fact]
    public async Task UnregisterDevice_RemovesOnlyThatToken()
    {
        var profile = Profile(Uid);
        profile.DeviceTokens.Add(new DeviceToken { Token = Token });
        profile.DeviceTokens.Add(new DeviceToken { Token = "second-device" });
        var users = new FakeRepository<UserProfile>(profile);

        var result = await ControllerFor(users).UnregisterDevice(new UsersController.DeviceTokenRequest(Token));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("second-device", Assert.Single(users.Documents[Uid].DeviceTokens).Token);
    }

    [Fact]
    public async Task UnregisterDevice_SucceedsWithoutWriting_WhenTheTokenIsNotHeld()
    {
        var users = new FakeRepository<UserProfile>(Profile(Uid));

        var result = await ControllerFor(users).UnregisterDevice(new UsersController.DeviceTokenRequest(Token));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, users.WriteCount);
    }

    [Fact]
    public async Task UnregisterDevice_CannotRemoveAnotherUsersToken()
    {
        var other = Profile("user-2");
        other.DeviceTokens.Add(new DeviceToken { Token = Token });
        var users = new FakeRepository<UserProfile>(Profile(Uid), other);

        await ControllerFor(users).UnregisterDevice(new UsersController.DeviceTokenRequest(Token));

        Assert.Single(users.Documents["user-2"].DeviceTokens);
    }

    [Fact]
    public void DeviceTokens_AreNeverSerialisedToClients()
    {
        var profile = Profile(Uid);
        profile.DeviceTokens.Add(new DeviceToken { Token = Token });

        var json = System.Text.Json.JsonSerializer.Serialize(profile);

        Assert.DoesNotContain(Token, json);
        Assert.DoesNotContain("deviceTokens", json, StringComparison.OrdinalIgnoreCase);
    }

    private static UserProfile Profile(string id) =>
        new() { Id = id, UserId = id, DisplayName = "Test User", Role = "Farmer" };

    private static UsersController ControllerFor(FakeRepository<UserProfile> users) =>
        new(users) { ControllerContext = SignedInAs.User(Uid) };
}
