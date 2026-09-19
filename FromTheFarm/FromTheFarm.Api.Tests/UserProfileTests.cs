using FromTheFarm.Api.Models;
using Xunit;

namespace FromTheFarm.Api.Tests;

public class UserProfileTests
{
    [Fact]
    public void OnboardingComplete_IsFalse_WhenRoleHasNotBeenSetYet()
    {
        var profile = new UserProfile { Role = null };

        Assert.False(profile.OnboardingComplete);
    }

    [Theory]
    [InlineData("Farmer")]
    [InlineData("Buyer")]
    public void OnboardingComplete_IsTrue_OnceARoleIsSet(string role)
    {
        var profile = new UserProfile { Role = role };

        Assert.True(profile.OnboardingComplete);
    }
}