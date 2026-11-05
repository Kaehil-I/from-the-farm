using FromTheFarm.Api.Models;

namespace FromTheFarm.Api.Services;

public enum MatchNotification
{
    NewMatch,
    Confirmed,
    Completed
}

// Decides whether a user should be told something and builds the message in
// their language. Delivery is best-effort by design: a failure to notify must
// never fail the request that caused it (a confirmed match stays confirmed),
// so everything here is caught and logged rather than thrown.
public class NotificationService
{
    private readonly IMongoRepository<UserProfile> _users;
    private readonly IPushSender _sender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IMongoRepository<UserProfile> users,
        IPushSender sender,
        ILogger<NotificationService> logger)
    {
        _users = users;
        _sender = sender;
        _logger = logger;
    }

    public async Task NotifyAsync(string userId, MatchNotification kind, MatchDocument match)
    {
        try
        {
            var profile = await _users.GetByIdAsync(userId);

            // Opting out in Settings is honoured here, on the server, so it holds
            // no matter which client or build is installed on the device.
            if (profile is null || !profile.NotificationsEnabled || profile.DeviceTokens.Count == 0)
            {
                return;
            }

            var (title, body) = NotificationMessages.For(kind, profile.Language, match.CounterpartSnapshot.CropType);
            var message = new PushMessage(title, body, new Dictionary<string, string>
            {
                ["type"] = NotificationMessages.TypeOf(kind),
                ["matchId"] = match.Id
            });

            var dead = await _sender.SendAsync(profile.DeviceTokens.Select(t => t.Token).ToList(), message);

            if (dead.Count > 0)
            {
                profile.DeviceTokens.RemoveAll(t => dead.Contains(t.Token));
                await _users.UpsertAsync(profile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send a {Kind} notification to user {UserId}.", kind, userId);
        }
    }
}
