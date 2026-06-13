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

    private enum Define { }   // ids cast from probe index
    private enum Request { }

    private readonly MessageWindow _window;
    private readonly System.Windows.Forms.Timer _reconnectTimer;
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

    public SimConnectBridge()
    {
        _window = new MessageWindow(HandleMessage);
        _reconnectTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _reconnectTimer.Tick += (_, _) => TryConnect();
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
        int i = (int)data.dwRequestID;
        if (i < 0 || i >= AdfProbes.Length) return;
        var value = ((DoubleData)data.dwData[0]).Value;
        var name = AdfProbes[i].Name;
        Log.Info($"ADF: {name} = {value}");

        if (name == "CIRCUIT AVIONICS ON")
        {
            _avionicsPower = value != 0;
            RecomputeGate();
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
