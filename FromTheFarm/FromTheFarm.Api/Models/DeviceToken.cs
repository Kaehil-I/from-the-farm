using System.Text.Json.Serialization;

namespace FromTheFarm.Api.Models;

// One Firebase Cloud Messaging registration token, embedded in the owning
// UserProfile. A user can be signed in on several devices, so they hold a list.
public class DeviceToken
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("registeredAt")]
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    // Refreshed every time the device re-registers, which is how the oldest
    // (most likely abandoned) token is chosen for eviction at the cap.
    [JsonPropertyName("lastSeenAt")]
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
