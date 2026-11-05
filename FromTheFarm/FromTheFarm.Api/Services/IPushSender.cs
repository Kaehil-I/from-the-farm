namespace FromTheFarm.Api.Services;

public record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data);

// The seam between "something worth telling a user happened" and Firebase Cloud
// Messaging. Controllers and NotificationService depend on this, so they can be
// tested without credentials and the API still starts where none are configured.
public interface IPushSender
{
    // Returns the tokens FCM reported as permanently unusable (the app was
    // uninstalled or the token expired), so the caller can stop storing them.
    Task<IReadOnlyList<string>> SendAsync(
        IReadOnlyCollection<string> tokens,
        PushMessage message,
        CancellationToken cancellationToken = default);
}

// Used when no Firebase service account is configured: notifications are
// switched off rather than the whole API failing to start.
public sealed class NullPushSender : IPushSender
{
    public Task<IReadOnlyList<string>> SendAsync(
        IReadOnlyCollection<string> tokens,
        PushMessage message,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
