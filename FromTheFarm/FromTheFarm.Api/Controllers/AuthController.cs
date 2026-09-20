using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace FromTheFarm.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly MongoRepository<UserProfile> _users;

    public AuthController(MongoRepository<UserProfile> users)
    {
        _users = users;
    }

    public record SessionRequest(string? DeviceLanguage);

    public record SessionResponse(
        string UserId,
        bool IsNewUser,
        string? Role,
        bool OnboardingComplete,
        ProfileSummary Profile);

    public record ProfileSummary(string DisplayName, string Language, int SearchRadiusKm);

    // The JWT bearer middleware has already validated the Firebase ID token
    // by the time this action runs — [Authorize] here just requires that a
    // valid token was presented at all. This endpoint's job is purely to
    // create-or-fetch the corresponding app profile document.
    [HttpPost("session")]
    [Authorize]
    public async Task<ActionResult<SessionResponse>> ExchangeSession([FromBody] SessionRequest request)
    {
        var uid = User.GetFirebaseUid();
        var existing = await _users.GetByIdAsync(uid);

        if (existing is not null)
        {
            return Ok(new SessionResponse(
                existing.UserId,
                IsNewUser: false,
                existing.Role,
                existing.OnboardingComplete,
                new ProfileSummary(existing.DisplayName, existing.Language, existing.SearchRadiusKm)));
        }

        var displayName = User.FindFirst("name")?.Value ?? "New user";

        var newProfile = new UserProfile
        {
            Id = uid,
            UserId = uid,
            DisplayName = displayName,
            Language = request.DeviceLanguage ?? "en",
            Role = null
        };

        await _users.UpsertAsync(newProfile);

        return Ok(new SessionResponse(
            newProfile.UserId,
            IsNewUser: true,
            newProfile.Role,
            newProfile.OnboardingComplete,
            new ProfileSummary(newProfile.DisplayName, newProfile.Language, newProfile.SearchRadiusKm)));
    }
}
