using Microsoft.Win32;

namespace MsfsMediaPlayer.Companion;

/// <summary>
/// Run-at-login toggle via the per-user registry Run key (no admin needed). The companion idles and
/// auto-reconnects, so starting at login means it's ready whenever the sim launches.
/// </summary>
internal static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MsfsMediaPlayer";

    private static string? ExePath => Environment.ProcessPath;

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string v && v.Length > 0;
        }
        catch (Exception ex)
        {
            Log.Warn($"Autostart read failed: {ex.Message}");
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled && ExePath is { Length: > 0 })
                key.SetValue(ValueName, $"\"{ExePath}\"");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            Log.Info($"Autostart {(enabled ? "enabled" : "disabled")}");
        }
        catch (Exception ex)
        {
            Log.Error($"Autostart toggle failed: {ex.Message}");
        }
    }
}
