using System.Runtime.InteropServices;

namespace MsfsMediaPlayer.Companion;

/// <summary>
/// Global hardware-media-key injection. Fallback for players that don't register with SMTC
/// (e.g. Qobuz): the OS routes these to whatever app currently owns media-key priority.
/// Transport only — no metadata, no targeting.
/// </summary>
internal static class MediaKeys
{
    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;
    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001; // media keys are extended keys
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public static void PlayPause() => Tap(VK_MEDIA_PLAY_PAUSE);
    public static void Next() => Tap(VK_MEDIA_NEXT_TRACK);
    public static void Previous() => Tap(VK_MEDIA_PREV_TRACK);

    private static void Tap(byte vk)
    {
        keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
        keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        Log.Info($"Media key sent: 0x{vk:X2}");
    }
}
