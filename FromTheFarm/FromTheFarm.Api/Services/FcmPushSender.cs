using FirebaseAdmin.Messaging;

namespace FromTheFarm.Api.Services;

// Sends through the FCM HTTP v1 API using the Firebase Admin SDK. The default
// FirebaseApp must already have been created from a service account (see
// Program.cs).
public sealed class FcmPushSender : IPushSender
{
    // The Android app registers a channel with this id. Android 8+ drops
    // notifications posted to a channel that does not exist, so the two must
    // agree.
    public const string ChannelId = "matches";

    public async Task<IReadOnlyList<string>> SendAsync(
        IReadOnlyCollection<string> tokens,
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        if (tokens.Count == 0)
        {
            return Array.Empty<string>();
        }

        var ordered = tokens.ToList();
        // Message.Token is flagged obsolete in favour of installation IDs, but the
        // Android SDK still issues registration tokens, which is what is stored.
#pragma warning disable CS0618
        var messages = ordered.Select(token => new Message
        {
            Token = token,
            Notification = new Notification { Title = message.Title, Body = message.Body },
            Data = message.Data.ToDictionary(kv => kv.Key, kv => kv.Value),
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                Notification = new AndroidNotification { ChannelId = ChannelId }
            }
        }).ToList();
#pragma warning restore CS0618

        var batch = await FirebaseMessaging.DefaultInstance.SendEachAsync(messages, cancellationToken);

        var dead = new List<string>();
        for (var i = 0; i < batch.Responses.Count; i++)
        {
            var response = batch.Responses[i];
            if (!response.IsSuccess && response.Exception?.MessagingErrorCode is MessagingErrorCode.Unregistered)
            {
                dead.Add(ordered[i]);
            }
        }

        return dead;
    }
}
