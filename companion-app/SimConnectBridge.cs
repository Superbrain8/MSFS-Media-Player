using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.FlightSimulator.SimConnect;

namespace MsfsMediaPlayer.Companion;

/// <summary>
/// SimConnect connection to MSFS. P3a: connect, stay connected, auto-reconnect when the sim
/// isn't running yet, and notice when it quits. ADF data requests (P3b) and radio gating (P3c)
/// build on this. Must run on the UI thread — SimConnect signals via a Win32 window message.
/// </summary>
internal sealed class SimConnectBridge : IDisposable
{
    // Custom message SimConnect posts to our window when data is waiting.
    private const int WM_USER_SIMCONNECT = 0x0402;

    // P3b discovery probes: each var gets its own definition+request (index = id), so an
    // unrecognised name only fails that one probe (logged via OnRecvException), not all of them.
    // CIRCUIT AVIONICS ON is the var that actually tracks avionics-bus power (verified live in the
    // C172 — AVIONICS MASTER SWITCH does NOT). True only when battery + avionics master + breaker
    // all supply the bus. Single clean gate signal. Order matters: index = request/define id.
    private static readonly (string Name, string Unit)[] AdfProbes =
    [
        ("CIRCUIT AVIONICS ON", "Bool"),
    ];

    [StructLayout(LayoutKind.Sequential)]
    private struct DoubleData { public double Value; }

    private enum Define { }   // ids cast from int (probe index / the DEF_* constants below)
    private enum Request { }
    private enum SysEvent { FlightLoaded = 1 } // L:vars reset on flight load → re-push bridge state

    // --- EFB↔companion LVAR bridge (P4). Names mirror efb-app/.../bridge/MediaBridge.ts. ---
    // Define/request ids kept clear of the probe index range (0..AdfProbes.Length-1).
    private const int DEF_CMD = 10;                    // read: command pulse from EFB
    private const int DEF_VOL = 11;                    // read: radio volume 0..100 from EFB
    private const int DEF_STATUS_RADIO_PLAYING = 20;   // write: status to EFB
    private const int DEF_STATUS_RADIO_IDX = 21;
    private const int DEF_STATUS_LOCAL_PLAYING = 22;
    private const int DEF_STATUS_GATE = 23;

    private const string LVAR_CMD = "L:MEDIAPLAYER_CMD";
    private const string LVAR_VOL = "L:MEDIAPLAYER_RADIO_VOL";
    private const string LVAR_RADIO_PLAYING = "L:MEDIAPLAYER_RADIO_PLAYING";
    private const string LVAR_RADIO_IDX = "L:MEDIAPLAYER_RADIO_IDX";
    private const string LVAR_LOCAL_PLAYING = "L:MEDIAPLAYER_LOCAL_PLAYING";
    private const string LVAR_GATE = "L:MEDIAPLAYER_GATE";

    // Text packed into numeric LVARs (LVARs are the only EFB-readable channel; the EFB sandbox can't
    // read SimConnect client data). 6 Latin-1 chars per double. Encoding mirrored in MediaBridge.ts.
    private const int CHARS_PER_SLOT = 6;

    // Now-playing text: 16 slots × 6 = 96 chars.
    private const int NP_SLOTS = 16;
    private const int DEF_NP_BASE = 30; // defs 30..45
    private string _lastNp = "";

    // Station list: count + up to 12 names × 6 slots (36 chars each). Pushed to the EFB on connect.
    private const int MAX_STATIONS_TX = 12;
    private const int NAME_SLOTS = 6;
    private const int DEF_STATION_COUNT = 50;
    private const int DEF_STATION_BASE = 51; // defs 51..(51 + 12*6 - 1)
    private const string LVAR_STATION_COUNT = "L:MEDIAPLAYER_STATION_COUNT";
    private const string LVAR_STN_PREFIX = "L:MEDIAPLAYER_STN";
    private string[] _stationNames = Array.Empty<string>();

    private bool _lvarsReady;

    private readonly MessageWindow _window;
    private readonly System.Windows.Forms.Timer _reconnectTimer;
    // One-shot re-push a couple seconds after FlightLoaded, in case the event fires before MSFS
    // finishes zeroing the L:vars (the immediate re-push would then be wiped).
    private readonly System.Windows.Forms.Timer _repushTimer;
    private int _repushesLeft;
    private SimConnect? _sc;
    private bool _disposed;

    public bool Connected { get; private set; }

    // Latest power state, updated as probe values arrive.
    private bool _avionicsPower;
    private float _gate = 1f;

    /// <summary>Raised (on the UI thread) when the sim connection comes up or goes down.</summary>
    public event Action<bool>? ConnectionChanged;

    /// <summary>
    /// ADF-derived gain 0..1 for the radio stream (power gate × ADF volume knob). Raised on the
    /// UI thread as ADF state changes. On disconnect, resets to 1 (plain radio out of sim).
    /// </summary>
    public event Action<float>? AdfGateChanged;

    /// <summary>Raised (UI thread) when the EFB writes a command code (see Cmd.* in MediaBridge.ts).</summary>
    public event Action<int>? CommandReceived;

    /// <summary>Raised (UI thread) when the EFB writes a radio volume 0..100.</summary>
    public event Action<int>? VolumeReceived;

    public SimConnectBridge()
    {
        _window = new MessageWindow(HandleMessage);
        _reconnectTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _reconnectTimer.Tick += (_, _) => TryConnect();
        _repushTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _repushTimer.Tick += (_, _) => RepushTick();
    }

    public void Start()
    {
        TryConnect();
        _reconnectTimer.Start(); // keep retrying; the sim may not be running yet
    }

    private void TryConnect()
    {
        if (_sc is not null) return;
        try
        {
            _sc = new SimConnect("MSFS Media Player", _window.Handle, WM_USER_SIMCONNECT, null, 0);
            _sc.OnRecvOpen += OnOpen;
            _sc.OnRecvQuit += OnQuit;
            _sc.OnRecvException += OnException;
            _sc.OnRecvSimobjectData += OnSimObjectData;
            _sc.OnRecvEvent += OnEvent;
            Log.Info("SimConnect: attempting connection");
        }
        catch (COMException)
        {
            // Sim not running yet — swallow; the reconnect timer will retry.
            _sc = null;
        }
        catch (Exception ex)
        {
            // Not a "sim offline" case — e.g. the managed SimConnect DLL failed to load.
            // Retrying won't help; log clearly and stop.
            Log.Error($"SimConnect connect failed (non-recoverable): {ex.GetType().Name}: {ex.Message}");
            _sc = null;
            _reconnectTimer.Stop();
        }
    }

    private void OnOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        Connected = true;
        Log.Info($"SimConnect: connected to {data.szApplicationName} v{data.dwApplicationVersionMajor}.{data.dwApplicationVersionMinor}");
        ConnectionChanged?.Invoke(true);
        SetupAdfProbes();
        SetupLvars();
        try { sender.SubscribeToSystemEvent(SysEvent.FlightLoaded, "FlightLoaded"); }
        catch (Exception ex) { Log.Warn($"FlightLoaded subscribe failed: {ex.Message}"); }
    }

    /// <summary>
    /// MSFS resets all L:vars to 0 when a flight (re)loads, but SimConnect stays connected (no
    /// OnQuit), so our once-on-connect pushes are lost. Re-push the bridge state on FlightLoaded.
    /// </summary>
    private void OnEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
    {
        if ((SysEvent)data.uEventID != SysEvent.FlightLoaded) return;
        Log.Info("SimConnect: FlightLoaded — re-pushing bridge state");
        ScheduleRepushes();
    }

    /// <summary>
    /// Push bridge state now, then repeatedly for ~10s. MSFS zeroes L:vars during/after a flight
    /// load over an interval that outlasts a single write, and the EFB panel can open at any point,
    /// so a few spaced re-pushes guarantee the station list/now-playing/gate land and stick.
    /// </summary>
    private void ScheduleRepushes()
    {
        RepushBridgeState();
        _repushesLeft = 5;        // 5 × 2s ≈ 10s of backups
        _repushTimer.Stop();
        _repushTimer.Start();
    }

    private void RepushTick()
    {
        RepushBridgeState();
        if (--_repushesLeft <= 0) _repushTimer.Stop();
    }

    private void RepushBridgeState()
    {
        if (!_lvarsReady) return;
        _lastNp = "";                                         // force NP re-push on next poll
        WriteStationList();                                   // re-push station list
        WriteLvar(DEF_STATUS_GATE, _gate >= 0.5f ? 1 : 0);   // re-push current gate
    }

    /// <summary>P4: register the EFB command/volume reads and the status writes.</summary>
    private void SetupLvars()
    {
        if (_sc is null) return;
        try
        {
            // Reads — request on change.
            DefineLvar(DEF_CMD, LVAR_CMD);
            _sc.RequestDataOnSimObject((Request)DEF_CMD, (Define)DEF_CMD, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0, 0, 0);
            DefineLvar(DEF_VOL, LVAR_VOL);
            _sc.RequestDataOnSimObject((Request)DEF_VOL, (Define)DEF_VOL, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0, 0, 0);

            // Writes — define only (pushed via SetDataOnSimObject).
            DefineLvar(DEF_STATUS_RADIO_PLAYING, LVAR_RADIO_PLAYING);
            DefineLvar(DEF_STATUS_RADIO_IDX, LVAR_RADIO_IDX);
            DefineLvar(DEF_STATUS_LOCAL_PLAYING, LVAR_LOCAL_PLAYING);
            DefineLvar(DEF_STATUS_GATE, LVAR_GATE);
            for (int i = 0; i < NP_SLOTS; i++) DefineLvar(DEF_NP_BASE + i, $"L:MEDIAPLAYER_NP{i}");
            DefineLvar(DEF_STATION_COUNT, LVAR_STATION_COUNT);
            for (int n = 0; n < MAX_STATIONS_TX * NAME_SLOTS; n++)
                DefineLvar(DEF_STATION_BASE + n, $"{LVAR_STN_PREFIX}{n}");

            _lvarsReady = true;
            // Push state now and a few more times: on first start the companion often connects while
            // the flight is still loading, and MSFS keeps zeroing L:vars past the first write.
            ScheduleRepushes();
            Log.Info("SimConnect: LVAR bridge ready");
        }
        catch (Exception ex)
        {
            Log.Warn($"LVAR bridge setup failed: {ex.Message}");
        }
    }

    private void DefineLvar(int id, string name)
    {
        _sc!.AddToDataDefinition((Define)id, name, "number", SIMCONNECT_DATATYPE.FLOAT64,
            0f, SimConnect.SIMCONNECT_UNUSED);
        _sc.RegisterDataDefineStruct<DoubleData>((Define)id);
    }

    /// <summary>EFB → companion radio status.</summary>
    public void SetRadioStatus(bool playing, int stationIndex)
    {
        WriteLvar(DEF_STATUS_RADIO_PLAYING, playing ? 1 : 0);
        WriteLvar(DEF_STATUS_RADIO_IDX, stationIndex);
    }

    /// <summary>EFB → companion local-media status.</summary>
    public void SetLocalStatus(bool playing) => WriteLvar(DEF_STATUS_LOCAL_PLAYING, playing ? 1 : 0);

    /// <summary>
    /// Push now-playing text to the EFB, packed as 6 Latin-1 chars per LVAR double (10 LVARs → 60
    /// chars). Char 0 terminates. Decoded identically in MediaBridge.ts.
    /// </summary>
    public void SetNowPlayingText(string text)
    {
        if (text == _lastNp) return;
        _lastNp = text;
        Log.Info($"NP text → '{text}' (lvarsReady={_lvarsReady})");
        WritePackedString(DEF_NP_BASE, NP_SLOTS, text);
    }

    /// <summary>Set the station list shown in the EFB; pushed now if connected, else on next connect.</summary>
    public void SetStationList(IEnumerable<string> names)
    {
        _stationNames = names.Take(MAX_STATIONS_TX).ToArray();
        if (_lvarsReady) WriteStationList();
    }

    private void WriteStationList()
    {
        WriteLvar(DEF_STATION_COUNT, _stationNames.Length);
        for (int i = 0; i < _stationNames.Length; i++)
            WritePackedString(DEF_STATION_BASE + i * NAME_SLOTS, NAME_SLOTS, _stationNames[i]);
    }

    /// <summary>Pack a string into <paramref name="slots"/> consecutive LVAR defs, 6 Latin-1 chars each.</summary>
    private void WritePackedString(int baseDefId, int slots, string text)
    {
        var bytes = System.Text.Encoding.Latin1.GetBytes(text); // unmappable chars → '?'
        for (int slot = 0; slot < slots; slot++)
        {
            double value = 0;
            double mul = 1;
            for (int k = 0; k < CHARS_PER_SLOT; k++)
            {
                int idx = slot * CHARS_PER_SLOT + k;
                byte b = idx < bytes.Length ? bytes[idx] : (byte)0;
                value += b * mul;
                mul *= 256;
            }
            WriteLvar(baseDefId + slot, value);
        }
    }

    private void WriteLvar(int defId, double value)
    {
        if (!_lvarsReady || _sc is null) return;
        try
        {
            _sc.SetDataOnSimObject((Define)defId, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_DATA_SET_FLAG.DEFAULT, new DoubleData { Value = value });
        }
        catch (Exception ex)
        {
            Log.Warn($"WriteLvar def {defId} failed: {ex.Message}");
        }
    }

    /// <summary>P3b: register each ADF probe var and request it once per second when it changes.</summary>
    private void SetupAdfProbes()
    {
        if (_sc is null) return;
        for (int i = 0; i < AdfProbes.Length; i++)
        {
            var (name, unit) = AdfProbes[i];
            try
            {
                _sc.AddToDataDefinition((Define)i, name, unit, SIMCONNECT_DATATYPE.FLOAT64,
                    0f, SimConnect.SIMCONNECT_UNUSED);
                _sc.RegisterDataDefineStruct<DoubleData>((Define)i);
                _sc.RequestDataOnSimObject((Request)i, (Define)i, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0, 0, 0);
            }
            catch (Exception ex)
            {
                Log.Warn($"ADF probe '{name}' setup failed: {ex.Message}");
            }
        }
        Log.Info($"SimConnect: requested {AdfProbes.Length} ADF probe vars (logging on change)");
    }

    private void OnSimObjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        int id = (int)data.dwRequestID;
        double value = ((DoubleData)data.dwData[0]).Value;

        // EFB → companion bridge reads.
        if (id == DEF_CMD)
        {
            int code = (int)value;
            if (code != 0)
            {
                Log.Info($"LVAR command: {code}");
                CommandReceived?.Invoke(code);
                WriteLvar(DEF_CMD, 0); // consume so the same command isn't re-fired
            }
            return;
        }
        if (id == DEF_VOL)
        {
            VolumeReceived?.Invoke((int)Math.Clamp(value, 0, 100));
            return;
        }

        // ADF/power probes.
        if (id >= 0 && id < AdfProbes.Length)
        {
            var name = AdfProbes[id].Name;
            Log.Info($"ADF: {name} = {value}");
            if (name == "CIRCUIT AVIONICS ON")
            {
                _avionicsPower = value != 0;
                RecomputeGate();
            }
        }
    }

    // Gate is power-only (avionics bus powered): universal across aircraft. Volume is controlled
    // from the EFB panel / tray, not the ADF knob (many aircraft don't wire ADF VOLUME).
    private void RecomputeGate()
    {
        float gate = _avionicsPower ? 1f : 0f;
        if (Math.Abs(gate - _gate) < 0.001f) return;
        _gate = gate;
        Log.Info($"ADF gate → {gate:0.00} (avionics powered={_avionicsPower})");
        AdfGateChanged?.Invoke(gate);
        WriteLvar(DEF_STATUS_GATE, gate >= 0.5f ? 1 : 0);
    }

    private void OnQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Log.Info("SimConnect: sim quit");
        Disconnect();
    }

    private void OnException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        => Log.Warn($"SimConnect exception: {(SIMCONNECT_EXCEPTION)data.dwException}");

    private void HandleMessage(ref Message m)
    {
        if (m.Msg != WM_USER_SIMCONNECT) return;
        try { _sc?.ReceiveMessage(); }
        catch (Exception ex)
        {
            Log.Warn($"SimConnect ReceiveMessage failed: {ex.Message}");
            Disconnect();
        }
    }

    private void Disconnect()
    {
        bool wasConnected = Connected || _sc is not null;
        Connected = false;
        _lvarsReady = false;
        try { _sc?.Dispose(); } catch { /* best effort */ }
        _sc = null;
        if (wasConnected)
        {
            ConnectionChanged?.Invoke(false);
            _gate = 1f;
            AdfGateChanged?.Invoke(1f); // out of sim: plain radio at user volume
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reconnectTimer.Stop();
        _reconnectTimer.Dispose();
        _repushTimer.Stop();
        _repushTimer.Dispose();
        Disconnect();
        _window.ReleaseHandle();
    }

    /// <summary>Message-only window so SimConnect has an HWND to post to without a visible form.</summary>
    private sealed class MessageWindow : NativeWindow
    {
        public delegate void MessageHandler(ref Message m);
        private readonly MessageHandler _handler;

        public MessageWindow(MessageHandler handler)
        {
            _handler = handler;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            _handler(ref m);
            base.WndProc(ref m);
        }
    }
}
