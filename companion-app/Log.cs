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

    public static void Init()
    {
        _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MsfsMediaPlayer", "logs");
        Directory.CreateDirectory(_logDir);
        _logFile = Path.Combine(_logDir, $"companion-{DateTime.Now:yyyyMMdd-HHmmss}.log");
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
