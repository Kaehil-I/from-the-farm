using System.Text.RegularExpressions;

namespace FromTheFarm.Api.Services;

// Builds and reads the public URL a listing keeps for its stored photo.
public static class PhotoLinks
{
    public const string Route = "/api/v1/photos/";

    private static readonly Regex IdInUrl = new(@"/api/v1/photos/([0-9a-fA-F]{32})$", RegexOptions.Compiled);

    // The app's photo loader only accepts https, and behind a TLS-terminating
    // proxy the request itself looks like http. So the scheme is forced to https
    // for any real host; localhost keeps http for development.
    public static string For(HttpRequest request, string id)
    {
        if (!request.Host.HasValue)
        {
            return Route + id;
        }

        var host = request.Host.Host;
        var local = host is "localhost" or "127.0.0.1" or "::1" or "10.0.2.2";
        return $"{(local ? request.Scheme : "https")}://{request.Host}{Route}{id}";
    }

    // The id of a stored photo, or null for anything else (no photo, or a legacy
    // inline data URI that has no blob to clean up).
    public static string? IdFrom(string? photoUrl)
    {
        if (string.IsNullOrEmpty(photoUrl))
        {
            return null;
        }
        var match = IdInUrl.Match(photoUrl);
        return match.Success ? match.Groups[1].Value : null;
    }
}
