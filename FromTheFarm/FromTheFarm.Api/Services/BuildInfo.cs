namespace FromTheFarm.Api.Services;

public static class BuildInfo
{
    // Render sets RENDER_GIT_COMMIT to the commit it built. Surfacing it on the
    // health check lets anyone confirm which version is live with a single request.
    public static string Commit { get; } = ShortCommit(Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT"));

    public static string ShortCommit(string? sha)
    {
        var trimmed = sha?.Trim();
        return string.IsNullOrEmpty(trimmed) ? "unknown" : trimmed[..Math.Min(7, trimmed.Length)];
    }
}
