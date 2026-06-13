using System.Windows.Forms;

namespace MsfsMediaPlayer.Companion;

/// <summary>
/// Owns the tray icon and its menu. The app has no main window — the tray is the whole UI
/// surface for now. Later phases hang SimConnect state off this context.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    // NotifyIcon.Text throws above this length on older shells; truncate to stay safe.
    private const int MaxTooltip = 63;

    private readonly NotifyIcon _trayIcon;

    private readonly MediaController _media = new();
    private readonly ToolStripMenuItem _nowPlayingItem;
    private readonly ToolStripMenuItem _playPauseItem;
    private readonly ToolStripMenuItem _nextItem;
    private readonly ToolStripMenuItem _prevItem;

    private readonly RadioPlayer _radio = new();
    private readonly ToolStripMenuItem _radioStatusItem;
    private readonly ToolStripMenuItem _stopRadioItem;

    private readonly SimConnectBridge _sim = new();
    private readonly ToolStripMenuItem _simStatusItem;

    private readonly IReadOnlyList<RadioStation> _stations = StationStore.Load();

    private SynchronizationContext? _ui;

    public TrayApplicationContext()
    {
        // --- Local media (SMTC / media keys) ---
        _nowPlayingItem = new ToolStripMenuItem("No media playing") { Enabled = false };
        _playPauseItem = new ToolStripMenuItem("Play / Pause", null, (_, _) => _ = _media.TogglePlayPauseAsync());
        _nextItem = new ToolStripMenuItem("Next", null, (_, _) => _ = _media.NextAsync());
        _prevItem = new ToolStripMenuItem("Previous", null, (_, _) => _ = _media.PreviousAsync());

        // --- Radio ---
        _radioStatusItem = new ToolStripMenuItem("Radio: stopped") { Enabled = false };
        _stopRadioItem = new ToolStripMenuItem("Stop radio", null, (_, _) => _radio.Stop()) { Enabled = false };

        var stationsMenu = new ToolStripMenuItem("Play station");
        for (int i = 0; i < _stations.Count; i++)
        {
            int idx = i; // capture per-iteration
            stationsMenu.DropDownItems.Add(new ToolStripMenuItem(_stations[idx].Name, null, (_, _) => PlayStation(idx)));
        }

        var volumeMenu = new ToolStripMenuItem("Radio volume");
        foreach (var pct in new[] { 25, 50, 75, 100 })
        {
            int p = pct;
            volumeMenu.DropDownItems.Add(new ToolStripMenuItem($"{p}%", null, (_, _) => _radio.Volume = p / 100f));
        }

        // --- Sim ---
        _simStatusItem = new ToolStripMenuItem("Sim: disconnected") { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_nowPlayingItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_prevItem);
        menu.Items.Add(_playPauseItem);
        menu.Items.Add(_nextItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_radioStatusItem);
        menu.Items.Add(stationsMenu);
        menu.Items.Add(volumeMenu);
        menu.Items.Add(_stopRadioItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_simStatusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open log folder", null, (_, _) => Log.OpenFolder());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "MSFS Media Player",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _trayIcon.ShowBalloonTip(2000, "MSFS Media Player", "Companion running.", ToolTipIcon.Info);

        // Defer SMTC init until the WinForms message loop is pumping, so SynchronizationContext.Current
        // is the WindowsFormsSynchronizationContext we can marshal WinRT/NAudio callbacks back onto.
        var startup = new System.Windows.Forms.Timer { Interval = 1 };
        startup.Tick += async (_, _) =>
        {
            startup.Stop();
            startup.Dispose();
            _ui = SynchronizationContext.Current;
            _media.Changed += OnMediaChanged;
            _radio.StatusChanged += OnRadioChanged;
            _sim.ConnectionChanged += OnSimChanged;
            _sim.AdfGateChanged += g => _ui?.Post(_ => _radio.AdfGate = g, null);
            _sim.CommandReceived += code => _ui?.Post(_ => DispatchCommand(code), null);
            _sim.VolumeReceived += v => _ui?.Post(_ => _radio.Volume = v / 100f, null);
            _sim.Start();
            try { await _media.InitAsync(); }
            catch (Exception ex) { Log.Error($"MediaController init failed: {ex.Message}"); }
        };
        startup.Start();
    }

    private void OnMediaChanged(NowPlaying np) => _ui?.Post(_ => RenderMedia(np), null);
    private void OnRadioChanged(RadioStatus rs) => _ui?.Post(_ => RenderRadio(rs), null);
    private void OnSimChanged(bool connected)
        => _ui?.Post(_ => _simStatusItem.Text = connected ? "Sim: connected" : "Sim: disconnected", null);

    // Command codes mirror Cmd.* in efb-app/.../bridge/MediaBridge.ts.
    private void DispatchCommand(int code)
    {
        switch (code)
        {
            case 1: _ = _media.TogglePlayPauseAsync(); break;
            case 2: _ = _media.NextAsync(); break;
            case 3: _ = _media.PreviousAsync(); break;
            case 10: _radio.Stop(); break;
            case >= 100: PlayStation(code - 100); break;
            default: Log.Warn($"Unknown EFB command code: {code}"); break;
        }
    }

    private void PlayStation(int index)
    {
        if (index < 0 || index >= _stations.Count) { Log.Warn($"Station index out of range: {index}"); return; }
        _ = _radio.PlayAsync(_stations[index]);
    }

    private void RenderMedia(NowPlaying np)
    {
        // Transport always works: SMTC when a session exists, global media keys otherwise.
        _playPauseItem.Enabled = _nextItem.Enabled = _prevItem.Enabled = true;

        _nowPlayingItem.Text = np.HasSession ? np.Display : "No track info (media-key control)";

        var tip = np.HasSession
            ? $"{(np.IsPlaying ? "▶" : "⏸")} {np.Display}"
            : "MSFS Media Player — media-key control";
        _trayIcon.Text = tip.Length > MaxTooltip ? tip[..MaxTooltip] : tip;

        _sim.SetLocalStatus(np.HasSession && np.IsPlaying);
    }

    private void RenderRadio(RadioStatus rs)
    {
        _radioStatusItem.Text = rs.IsPlaying ? $"Radio: {rs.StationName}" : "Radio: stopped";
        _stopRadioItem.Enabled = rs.IsPlaying;

        int idx = rs.IsPlaying ? IndexOfStation(rs.StationName) : -1;
        _sim.SetRadioStatus(rs.IsPlaying, idx);
    }

    private int IndexOfStation(string name)
    {
        for (int i = 0; i < _stations.Count; i++)
            if (_stations[i].Name == name) return i;
        return -1;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sim.Dispose();
            _radio.Dispose();
            _media.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
