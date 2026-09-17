using System.Text.Json.Serialization;

namespace FromTheFarm.Api.Models;

// Cosmos container: Users. Partition key: /userId
public class UserProfile
{
    // Cosmos requires a document "id" — we reuse the Firebase UID for both.
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    // Referenced by GET /matches/{id}'s counterpartContact.phone in Section 5,
    // but no settings field for collecting it was ever specified — this is a
    // gap in the original design, not an oversight here. Flag to Zario: needs
    // a field in the settings/onboarding screen, and Firebase Auth's Google
    // Sign-In doesn't supply a phone number, so it must be entered manually.
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    // "Farmer" | "Buyer" | null while onboarding is incomplete
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("searchRadiusKm")]
    public int SearchRadiusKm { get; set; } = 10;

    [JsonPropertyName("notificationsEnabled")]
    public bool NotificationsEnabled { get; set; } = true;

    [JsonPropertyName("biometricLockEnabled")]
    public bool BiometricLockEnabled { get; set; } = false;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public bool OnboardingComplete => Role is not null;
}
