using NAudio.Wave;

namespace MsfsMediaPlayer.Companion;

/// <summary>State of the radio player, pushed to the UI on change.</summary>
internal sealed record RadioStatus(bool IsPlaying, string StationName)
{
    public static readonly RadioStatus Stopped = new(false, "");
}

/// <summary>
/// Plays an internet-radio stream through the default Windows audio device (NAudio). v1 = own
/// audio output; P3 layers ADF gating/attenuation on top. Stream open happens on a background
/// thread because the network connect can block.
/// </summary>
internal sealed class RadioPlayer : IDisposable
{
    private readonly object _gate = new();
    private WaveOutEvent? _output;
    private MediaFoundationReader? _reader;
    private RadioStation? _current;
    private float _userVolume = 0.6f;
    private float _adfGate = 1f;

    /// <summary>Raised on play/stop/failure. Fires on a background thread.</summary>
    public event Action<RadioStatus>? StatusChanged;

    /// <summary>User-set base volume 0.0–1.0.</summary>
    public float Volume
    {
        get => _userVolume;
        set { _userVolume = Math.Clamp(value, 0f, 1f); ApplyVolume(); }
    }

    /// <summary>ADF-derived gain 0.0–1.0 from the sim (power × ADF knob). 1.0 = no attenuation.</summary>
    public float AdfGate
    {
        get => _adfGate;
        set { _adfGate = Math.Clamp(value, 0f, 1f); ApplyVolume(); }
    }

    private void ApplyVolume()
    {
        lock (_gate) { if (_output is not null) _output.Volume = _userVolume * _adfGate; }
    }

    public async Task PlayAsync(RadioStation station)
    {
        Stop();
        Log.Info($"Radio: connecting to {station.Name} ({station.Url})");
        try
        {
            await Task.Run(() =>
            {
                var reader = new MediaFoundationReader(station.Url);
                var output = new WaveOutEvent();
                output.PlaybackStopped += OnPlaybackStopped;
                output.Init(reader);
                output.Volume = _userVolume * _adfGate;
                output.Play();
                lock (_gate)
                {
                    _reader = reader;
                    _output = output;
                    _current = station;
                }
            });
            Log.Info($"Radio: playing {station.Name}");
            StatusChanged?.Invoke(new RadioStatus(true, station.Name));
        }
        catch (Exception ex)
        {
            Log.Error($"Radio: failed to play {station.Name} — {ex.Message}");
            Stop();
        }
    }

    public void Stop()
    {
        WaveOutEvent? output;
        MediaFoundationReader? reader;
        lock (_gate)
        {
            output = _output;
            reader = _reader;
            _output = null;
            _reader = null;
            _current = null;
        }
        if (output is not null)
        {
            output.PlaybackStopped -= OnPlaybackStopped;
            output.Dispose();
        }
        reader?.Dispose();
        StatusChanged?.Invoke(RadioStatus.Stopped);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // Fires on stream end or error (e.g. dropped connection). v1: just stop; no auto-reconnect.
        if (e.Exception is not null)
            Log.Warn($"Radio: playback stopped with error — {e.Exception.Message}");
        Stop();
    }

    public void Dispose() => Stop();
}
