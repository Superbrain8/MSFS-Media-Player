using Windows.Media.Control;
using Sessions = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace MsfsMediaPlayer.Companion;

/// <summary>
/// Wraps Windows System Media Transport Controls (SMTC). Tracks whatever app currently holds the
/// system media session (Spotify, a browser, etc. — generic, no per-app targeting) and exposes
/// transport commands + a now-playing snapshot.
///
/// All WinRT callbacks arrive on threadpool threads; <see cref="Changed"/> is raised on those
/// threads, so subscribers must marshal to the UI thread themselves.
/// </summary>
internal sealed class MediaController : IDisposable
{
    private Sessions? _manager;
    private Session? _session;
    private bool _disposed;

    /// <summary>Fired whenever the active session, its track, or its playback state changes.</summary>
    public event Action<NowPlaying>? Changed;

    public NowPlaying Current { get; private set; } = NowPlaying.None;

    public async Task InitAsync()
    {
        _manager = await Sessions.RequestAsync();
        _manager.CurrentSessionChanged += OnCurrentSessionChanged;
        HookCurrentSession();
        Log.Info("MediaController initialised");
    }

    // Each command prefers the SMTC session (targeted + metadata). With no session, fall back to
    // a global media key for players that don't register SMTC (e.g. Qobuz).

    public async Task TogglePlayPauseAsync()
    {
        if (_session is null) { MediaKeys.PlayPause(); return; }
        try { await _session.TryTogglePlayPauseAsync(); }
        catch (Exception ex) { Log.Warn($"TogglePlayPause failed: {ex.Message}"); }
    }

    public async Task NextAsync()
    {
        if (_session is null) { MediaKeys.Next(); return; }
        try { await _session.TrySkipNextAsync(); }
        catch (Exception ex) { Log.Warn($"SkipNext failed: {ex.Message}"); }
    }

    public async Task PreviousAsync()
    {
        if (_session is null) { MediaKeys.Previous(); return; }
        try { await _session.TrySkipPreviousAsync(); }
        catch (Exception ex) { Log.Warn($"SkipPrevious failed: {ex.Message}"); }
    }

    private void OnCurrentSessionChanged(Sessions sender, CurrentSessionChangedEventArgs args)
        => HookCurrentSession();

    private void HookCurrentSession()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnSessionChanged;
            _session.PlaybackInfoChanged -= OnSessionChanged;
        }

        LogAllSessions();

        // Prefer the OS "current" session; if none is designated, fall back to the first known
        // session so apps that never grab system-current focus (some players) still get picked up.
        _session = _manager?.GetCurrentSession()
                   ?? _manager?.GetSessions().FirstOrDefault();

        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnSessionChanged;
            _session.PlaybackInfoChanged += OnSessionChanged;
            Log.Info($"Active media session: {_session.SourceAppUserModelId}");
        }
        else
        {
            Log.Info("No active media session");
        }

        _ = RefreshAsync();
    }

    private void LogAllSessions()
    {
        var sessions = _manager?.GetSessions();
        if (sessions is null || sessions.Count == 0)
        {
            Log.Info("SMTC sessions: (none) — no app is publishing media transport controls");
            return;
        }
        var ids = string.Join(", ", sessions.Select(s => s.SourceAppUserModelId));
        Log.Info($"SMTC sessions ({sessions.Count}): {ids}");
    }

    private void OnSessionChanged(Session sender, object args) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        var session = _session;
        if (session is null)
        {
            Publish(NowPlaying.None);
            return;
        }

        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            var playing = session.GetPlaybackInfo().PlaybackStatus
                == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            Publish(new NowPlaying(true, playing, props?.Title ?? "", props?.Artist ?? ""));
        }
        catch (Exception ex)
        {
            Log.Warn($"Reading media properties failed: {ex.Message}");
        }
    }

    private void Publish(NowPlaying np)
    {
        Current = np;
        Changed?.Invoke(np);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnSessionChanged;
            _session.PlaybackInfoChanged -= OnSessionChanged;
        }
        if (_manager is not null)
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
    }
}
