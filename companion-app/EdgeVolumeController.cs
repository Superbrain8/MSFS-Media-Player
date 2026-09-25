using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace MsfsMediaPlayer.Companion;

/// <summary>
/// Adjusts only Windows audio sessions owned by Microsoft Edge (msedge.exe), leaving MSFS,
/// Virtual Desktop Streamer and the master endpoint level untouched. This is a Windows mixer
/// control, not a browser-tab-specific or Amazon-specific volume control.
/// </summary>
internal sealed class EdgeVolumeController
{
    public void SetVolume(int percentage)
    {
        float volume = Math.Clamp(percentage, 0, 100) / 100f;
        int adjusted = 0;

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            // Edge can be routed to a non-default output device (e.g. Virtual Desktop Audio).
            // Visit each ACTIVE playback endpoint; never modify an endpoint's master volume.
            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var endpoint in endpoints)
            {
                using (endpoint)
                {
                    try
                    {
                        var sessions = endpoint.AudioSessionManager.Sessions;
                        for (int i = 0; i < sessions.Count; i++)
                        {
                            var session = sessions[i];
                            uint pid = session.GetProcessID;
                            if (pid == 0 || pid > int.MaxValue) continue;

                            try
                            {
                                using var process = Process.GetProcessById((int)pid);
                                if (!process.ProcessName.Equals("msedge", StringComparison.OrdinalIgnoreCase))
                                    continue;
                                session.SimpleAudioVolume.Volume = volume;
                                adjusted++;
                            }
                            catch (ArgumentException)
                            {
                                // Edge may have closed a renderer between enumerating sessions
                                // and resolving its PID. Skip the stale session.
                            }
                            catch (InvalidOperationException)
                            {
                                // Session or renderer disappeared during a browser navigation.
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"Edge volume: endpoint '{endpoint.FriendlyName}': {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Edge volume enumeration failed: {ex.Message}");
            return;
        }

        if (adjusted == 0)
            Log.Warn("Edge volume: no msedge.exe audio session found (start Amazon Music playback first)");
        else
            Log.Info($"Edge volume set to {percentage}% on {adjusted} audio session(s)");
    }
}
