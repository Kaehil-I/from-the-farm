using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FromTheFarm.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMongoRepository<UserProfile> _users;

    public UsersController(IMongoRepository<UserProfile> users)
    {
        _users = users;
    }

    public record UpdateProfileRequest(
        string Role,
        string Language,
        int SearchRadiusKm,
        bool NotificationsEnabled,
        bool BiometricLockEnabled,
        string? Phone);

    public record DeviceTokenRequest(string Token);

    [HttpGet("me")]
    public async Task<ActionResult<UserProfile>> GetMyProfile()
    {
        var uid = User.GetFirebaseUid();
        var profile = await _users.GetByIdAsync(uid);

        return profile is null ? NotFound() : Ok(profile);
    }

    // Also used to complete onboarding (role, language, radius) — per
    // Section 3, the same endpoint handles both cases.
    [HttpPut("me")]
    public async Task<ActionResult<UserProfile>> UpdateMyProfile([FromBody] UpdateProfileRequest request)
    {
        var invalid = RequestValidation.Role(request.Role)
            ?? RequestValidation.Language(request.Language)
            ?? RequestValidation.SearchRadiusKm(request.SearchRadiusKm)
            ?? RequestValidation.Phone(request.Phone);

        if (invalid is not null)
        {
            return BadRequest(invalid);
        }

        var uid = User.GetFirebaseUid();
        var existing = await _users.GetByIdAsync(uid);
        if (existing is null)
        {
            return NotFound("Call POST /auth/session before updating a profile.");
        }

        existing.Role = request.Role;
        existing.Language = request.Language;
        existing.SearchRadiusKm = request.SearchRadiusKm;
        existing.NotificationsEnabled = request.NotificationsEnabled;
        existing.BiometricLockEnabled = request.BiometricLockEnabled;

        // A cleared field arrives as an empty string from the form; store it as
        // absent so "no number shared" is one value rather than two.
        existing.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        var updated = await _users.UpsertAsync(existing);
        return Ok(updated);
    }

    // Called by the app after sign-in and again whenever Firebase rotates the
    // token, so it is an upsert: registering a token that is already held just
    // refreshes when it was last seen.
    [HttpPut("me/devices")]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceTokenRequest request)
    {
        var invalid = RequestValidation.DeviceToken(request.Token);
        if (invalid is not null)
        {
            return BadRequest(invalid);
        }

        var uid = User.GetFirebaseUid();
        var profile = await _users.GetByIdAsync(uid);
        if (profile is null)
        {
            return NotFound("Call POST /auth/session before registering a device.");
        }

        var token = request.Token.Trim();
        var now = DateTime.UtcNow;

        // A token identifies a device install, not a person. If someone else
        // signed in on this phone earlier, their account must stop receiving
        // notifications meant for whoever holds the phone now.
        var previousOwners = await _users.FindAsync(u => u.Id != uid && u.DeviceTokens.Any(t => t.Token == token));
        foreach (var other in previousOwners)
        {
            other.DeviceTokens.RemoveAll(t => t.Token == token);
            await _users.UpsertAsync(other);
        }

        var held = profile.DeviceTokens.FirstOrDefault(t => t.Token == token);
        if (held is not null)
        {
            held.LastSeenAt = now;
        }
        else
        {
            profile.DeviceTokens.Add(new DeviceToken { Token = token, RegisteredAt = now, LastSeenAt = now });

            var overflow = profile.DeviceTokens.Count - RequestValidation.MaxDevicesPerUser;
            if (overflow > 0)
            {
                foreach (var stale in profile.DeviceTokens.OrderBy(t => t.LastSeenAt).Take(overflow).ToList())
                {
                    profile.DeviceTokens.Remove(stale);
                }
            }
        }

        await _users.UpsertAsync(profile);
        return NoContent();
    }

    // Called on sign-out so a signed-out device stops receiving the account's
    // notifications. The token travels in the body rather than the URL to keep
    // it out of request logs. Removing a token that is not held is a success:
    // the end state the caller wanted already holds.
    [HttpDelete("me/devices")]
    public async Task<IActionResult> UnregisterDevice([FromBody] DeviceTokenRequest request)
    {
        var invalid = RequestValidation.DeviceToken(request.Token);
        if (invalid is not null)
        {
            return BadRequest(invalid);
        }

        var uid = User.GetFirebaseUid();
        var profile = await _users.GetByIdAsync(uid);
        if (profile is null)
        {
            return NotFound();
        }

        if (profile.DeviceTokens.RemoveAll(t => t.Token == request.Token.Trim()) > 0)
        {
            await _users.UpsertAsync(profile);
        }

        return NoContent();
    }
}
