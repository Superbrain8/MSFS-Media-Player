using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace MsfsMediaPlayer.Companion;

/// <summary>
/// Checks GitHub Releases for a newer version on startup. Unauthenticated (public repo, 60 req/hr
/// per IP is ample for a once-per-launch check). Fails silently when offline.
/// </summary>
internal static class UpdateChecker
{
    private const string LatestApi =
        "https://api.github.com/repos/Superbrain8/MSFS-Media-Player/releases/latest";
    public const string ReleasesUrl =
        "https://github.com/Superbrain8/MSFS-Media-Player/releases/latest";

    /// <summary>Running version, normalised to major.minor.build (assembly adds a .0 revision).</summary>
    public static Version Current =>
        Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

    /// <summary>Latest release tag if it's newer than <see cref="Current"/>, else null.</summary>
    public static async Task<(Version Version, string Tag)?> CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MSFS-Media-Player-Companion");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var json = await http.GetStringAsync(LatestApi);
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString();
            var latest = ParseTag(tag);
            if (latest is null) return null;

            return latest > Current ? (latest, tag!) : null;
        }
        catch (Exception ex)
        {
            Log.Info($"Update check skipped: {ex.Message}");
            return null;
        }
    }

    public static void OpenReleasesPage()
    {
        try { Process.Start(new ProcessStartInfo(ReleasesUrl) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn($"Opening releases page failed: {ex.Message}"); }
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    private static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var s = tag.TrimStart('v', 'V').Trim();
        return Version.TryParse(s, out var v) ? Normalize(v) : null;
    }
}
