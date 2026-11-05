using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using FromTheFarm.Api.Services;

namespace FromTheFarm.Api.Models;

// Mongo collection: Users
public class UserProfile : IDocument
{
    // Mongo requires a document "_id" — we reuse the Firebase UID for both.
    [BsonId]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    // Released to a counterpart through counterpartContact.phone on
    // GET /matches/{id} once a match is confirmed. Entered by the user in
    // Settings: Google Sign-In does not supply a phone number.
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

    // FCM registration tokens for this user's devices. Never returned to
    // clients: a token lets whoever holds it be sent notifications for this
    // account, and the app has no use for reading them back.
    [JsonIgnore]
    public List<DeviceToken> DeviceTokens { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    [BsonIgnore]
    public bool OnboardingComplete => Role is not null;
}
