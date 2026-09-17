using System.Security.Claims;

namespace FromTheFarm.Api.Services;

public static class ClaimsPrincipalExtensions
{
    // Firebase ID tokens carry the UID in the standard "sub" (subject) claim.
    // This is the identity source of truth for every endpoint below — no
    // endpoint accepts a user ID from the request body or path for the
    // authenticated user's own data, so one user can never query another's.
    public static string GetFirebaseUid(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? throw new InvalidOperationException("No 'sub' claim present on the validated token.");
    }
}
