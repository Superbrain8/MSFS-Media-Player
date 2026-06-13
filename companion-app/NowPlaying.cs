namespace MsfsMediaPlayer.Companion;

/// <summary>Snapshot of the active media session, marshalled to the UI thread for display.</summary>
internal sealed record NowPlaying(bool HasSession, bool IsPlaying, string Title, string Artist)
{
    public static readonly NowPlaying None = new(false, false, "", "");

    /// <summary>"Artist — Title", or just one side if the other is blank.</summary>
    public string Display =>
        !HasSession ? "No media playing"
        : (Artist, Title) switch
        {
            ("", "") => "Unknown track",
            ("", var t) => t,
            (var a, "") => a,
            var (a, t) => $"{a} — {t}",
        };
}
