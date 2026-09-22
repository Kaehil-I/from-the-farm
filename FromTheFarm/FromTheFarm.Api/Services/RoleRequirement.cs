using FromTheFarm.Api.Models;

namespace FromTheFarm.Api.Services;

// The Android app hides actions that don't suit the caller's role, but a hidden
// button is not access control: anyone with a valid token can call the API
// directly. Write endpoints reserved for one role check it here instead.
public static class RoleRequirement
{
    // Returns the reason the caller may not perform an action reserved for
    // `required`, or null when they may. `action` completes the sentence
    // "Only {required}s can {action}."
    public static string? Check(UserProfile? profile, string required, string action) =>
        profile?.Role switch
        {
            null => $"Choose a role before you {action}.",
            var role when string.Equals(role, required, StringComparison.Ordinal) => null,
            _ => $"Only {required.ToLowerInvariant()}s can {action}."
        };
}
