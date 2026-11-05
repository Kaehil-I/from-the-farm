namespace FromTheFarm.Api.Services;

// Notification wording for the three supported languages. Kept in code rather
// than looked up from the device because the push is composed on the server,
// from the language stored on the recipient's profile.
public static class NotificationMessages
{
    // Sent in the data payload; the app reads it to decide where a tap goes.
    public static string TypeOf(MatchNotification kind) => kind switch
    {
        MatchNotification.NewMatch => "match_new",
        MatchNotification.Confirmed => "match_confirmed",
        _ => "match_completed"
    };

    public static (string Title, string Body) For(MatchNotification kind, string language, string crop)
    {
        var lang = language is "zu" or "af" ? language : "en";

        return (kind, lang) switch
        {
            (MatchNotification.NewMatch, "zu") => ("Ukufana okusha", $"Kukhona ukufana okusha kwe-{crop} kukulindile."),
            (MatchNotification.NewMatch, "af") => ("Nuwe passing", $"'n Nuwe {crop}-passing wag vir jou."),
            (MatchNotification.NewMatch, _) => ("New match", $"A new {crop} match is waiting for you."),

            (MatchNotification.Confirmed, "zu") => ("Ukufana kuqinisekisiwe", $"Ukufana kwakho kwe-{crop} kuqinisekisiwe. Imininingwane yokuxhumana iyatholakala manje."),
            (MatchNotification.Confirmed, "af") => ("Passing bevestig", $"Jou {crop}-passing is bevestig. Kontakbesonderhede is nou beskikbaar."),
            (MatchNotification.Confirmed, _) => ("Match confirmed", $"Your {crop} match was confirmed. Contact details are now available."),

            (_, "zu") => ("Ukufana kuphelile", $"Ukushintshana kwakho kwe-{crop} kumakiwe kuphelile. Shiya isilinganiso."),
            (_, "af") => ("Passing voltooi", $"Jou {crop}-ruil is as voltooi gemerk. Los 'n gradering."),
            _ => ("Match completed", $"Your {crop} exchange was marked complete. Please leave a rating.")
        };
    }
}
