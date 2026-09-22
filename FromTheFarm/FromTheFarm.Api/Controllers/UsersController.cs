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
}
