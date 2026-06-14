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

    private readonly ToolStripMenuItem _updateItem;
    private bool _updateAvailable;

    private List<RadioStation> _stations = StationStore.Load().ToList();
    private readonly ToolStripMenuItem _stationsMenu = new("Play station");

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

        RebuildStationsMenu();

        var volumeMenu = new ToolStripMenuItem("Radio volume");
        foreach (var pct in new[] { 25, 50, 75, 100 })
        {
            int p = pct;
            volumeMenu.DropDownItems.Add(new ToolStripMenuItem($"{p}%", null, (_, _) => _radio.Volume = p / 100f));
        }

        // --- Sim ---
        _simStatusItem = new ToolStripMenuItem("Sim: disconnected") { Enabled = false };

        // Hidden until a newer release is found on GitHub (see CheckForUpdatesAsync).
        _updateItem = new ToolStripMenuItem("Update available", null, (_, _) => UpdateChecker.OpenReleasesPage())
        {
            Visible = false,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_updateItem);
        menu.Items.Add(_nowPlayingItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_prevItem);
        menu.Items.Add(_playPauseItem);
        menu.Items.Add(_nextItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_radioStatusItem);
        menu.Items.Add(_stationsMenu);
        menu.Items.Add(volumeMenu);
        menu.Items.Add(_stopRadioItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Edit stations…", null, (_, _) => EditStations());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_simStatusItem);
        menu.Items.Add(new ToolStripSeparator());
        var autoStartItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = AutoStart.IsEnabled(),
        };
        autoStartItem.CheckedChanged += (_, _) => AutoStart.SetEnabled(autoStartItem.Checked);
        menu.Items.Add(autoStartItem);
        menu.Items.Add("Open log folder", null, (_, _) => Log.OpenFolder());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _trayIcon = new NotifyIcon
        {
            Icon = AppIcon.CreateTrayIcon(AppIcon.Disconnected),
            Text = "MSFS Media Player",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _trayIcon.ShowBalloonTip(2000, "MSFS Media Player", "Companion running.", ToolTipIcon.Info);
        _trayIcon.BalloonTipClicked += (_, _) => { if (_updateAvailable) UpdateChecker.OpenReleasesPage(); };

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
            _sim.SetStationList(_stations.Select(s => s.Name)); // pushed to EFB on connect
            _sim.Start();
            _ = CheckForUpdatesAsync();
            try { await _media.InitAsync(); }
            catch (Exception ex) { Log.Error($"MediaController init failed: {ex.Message}"); }
        };
        startup.Start();
    }

    private async Task CheckForUpdatesAsync()
    {
        var result = await UpdateChecker.CheckAsync();
        if (result is null) return;
        _ui?.Post(_ =>
        {
            _updateAvailable = true;
            _updateItem.Text = $"Update available: {result.Value.Tag} ↗";
            _updateItem.Visible = true;
            _trayIcon.ShowBalloonTip(6000, "MSFS Media Player update",
                $"Version {result.Value.Tag} is available — click to download.", ToolTipIcon.Info);
            Log.Info($"Update available: {result.Value.Tag} (running {UpdateChecker.Current})");
        }, null);
    }

    private void OnMediaChanged(NowPlaying np) => _ui?.Post(_ => RenderMedia(np), null);
    private void OnRadioChanged(RadioStatus rs) => _ui?.Post(_ => RenderRadio(rs), null);
    private void OnSimChanged(bool connected) => _ui?.Post(_ =>
    {
        _simStatusItem.Text = connected ? "Sim: connected" : "Sim: disconnected";
        var old = _trayIcon.Icon;
        _trayIcon.Icon = AppIcon.CreateTrayIcon(connected ? AppIcon.Connected : AppIcon.Disconnected);
        old?.Dispose();
    }, null);

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

    private void RebuildStationsMenu()
    {
        _stationsMenu.DropDownItems.Clear();
        for (int i = 0; i < _stations.Count; i++)
        {
            int idx = i; // capture per-iteration
            _stationsMenu.DropDownItems.Add(new ToolStripMenuItem(_stations[idx].Name, null, (_, _) => PlayStation(idx)));
        }
        _stationsMenu.Enabled = _stations.Count > 0;
    }

    private void EditStations()
    {
        using var form = new StationsForm(_stations);
        if (form.ShowDialog() != DialogResult.OK) return;

        _stations = form.Stations;
        StationStore.Save(_stations);
        RebuildStationsMenu();
        _sim.SetStationList(_stations.Select(s => s.Name)); // live-push to the EFB
        Log.Info($"Station list updated ({_stations.Count})");
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
        // Em-dash isn't in Latin-1 (the packed-text encoding) and the EFB font boxes it anyway.
        _sim.SetNowPlayingText(np.HasSession ? np.Display.Replace('—', '-') : "");
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
