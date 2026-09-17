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
    private readonly CosmosRepository<UserProfile> _users;

    public UsersController(CosmosRepository<UserProfile> users)
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

    [HttpGet("me")]
    public async Task<ActionResult<UserProfile>> GetMyProfile()
    {
        var uid = User.GetFirebaseUid();
        var profile = await _users.GetByIdAsync(uid, uid);

        return profile is null ? NotFound() : Ok(profile);
    }

    // Also used to complete onboarding (role, language, radius) — per
    // Section 3, the same endpoint handles both cases.
    [HttpPut("me")]
    public async Task<ActionResult<UserProfile>> UpdateMyProfile([FromBody] UpdateProfileRequest request)
    {
        if (request.SearchRadiusKm is < 1 or > 100)
        {
            return BadRequest("searchRadiusKm must be between 1 and 100.");
        }

        var uid = User.GetFirebaseUid();
        var existing = await _users.GetByIdAsync(uid, uid);
        if (existing is null)
        {
            return NotFound("Call POST /auth/session before updating a profile.");
        }

        existing.Role = request.Role;
        existing.Language = request.Language;
        existing.SearchRadiusKm = request.SearchRadiusKm;
        existing.NotificationsEnabled = request.NotificationsEnabled;
        existing.BiometricLockEnabled = request.BiometricLockEnabled;
        existing.Phone = request.Phone;

        var updated = await _users.UpsertAsync(existing, uid);
        return Ok(updated);
    }
}
