using System.Diagnostics;
using System.Text;

namespace MsfsMediaPlayer.Companion;

/// <summary>
/// Dead-simple append-only file logger. No window/console (WinExe), so disk is the only
/// place to see what happened. One file per run under %LocalAppData%\MsfsMediaPlayer\logs.
/// </summary>
internal static class Log
{
    private static readonly object Gate = new();
    private static string _logDir = "";
    private static string _logFile = "";

    // Keep only the newest N run logs (one per launch) so the folder doesn't grow unbounded.
    private const int MaxLogFiles = 10;

    public static void Init()
    {
        _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MsfsMediaPlayer", "logs");
        Directory.CreateDirectory(_logDir);
        _logFile = Path.Combine(_logDir, $"companion-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        PruneOldLogs();
    }

    private static void PruneOldLogs()
    {
        try
        {
            var old = Directory.GetFiles(_logDir, "companion-*.log")
                .OrderByDescending(f => f)   // names are timestamp-sortable
                .Skip(MaxLogFiles);
            foreach (var f in old) File.Delete(f);
        }
        catch { /* pruning must never block startup */ }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}";
        Debug.WriteLine(line);
        if (string.IsNullOrEmpty(_logFile)) return;
        lock (Gate)
        {
            try { File.AppendAllText(_logFile, line + Environment.NewLine, Encoding.UTF8); }
            catch { /* logging must never crash the app */ }
        }
    }

    public static void OpenFolder()
    {
        if (string.IsNullOrEmpty(_logDir)) return;
        Process.Start(new ProcessStartInfo { FileName = _logDir, UseShellExecute = true });
    }
}
