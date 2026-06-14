using System.Text.Json;

namespace MsfsMediaPlayer.Companion;

/// <summary>
/// Loads the radio station list from JSON under %LocalAppData%\MsfsMediaPlayer\stations.json.
/// Writes a default list on first run. v1 is read-on-start; editing in the companion + EFB
/// selection comes later (see docs/DECISIONS.md).
/// </summary>
internal static class StationStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // Direct MP3 streams (work with MediaFoundationReader). Curated, reliable defaults.
    private static readonly RadioStation[] Defaults =
    [
        new("SomaFM — Groove Salad", "https://ice1.somafm.com/groovesalad-128-mp3"),
        new("SomaFM — Drone Zone", "https://ice1.somafm.com/dronezone-128-mp3"),
        new("Radio Paradise — Main Mix", "https://stream.radioparadise.com/mp3-128"),
        new("Radio Paradise — Mellow Mix", "https://stream.radioparadise.com/mellow-128"),
        new("Radio Bob — Harte Saite", "http://streams.radiobob.de/bob-hartesaite/mp3-192/mediaplayer"),
    ];

    public static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MsfsMediaPlayer", "stations.json");

    public static IReadOnlyList<RadioStation> Load()
    {
        var path = ConfigPath;
        try
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(Defaults, JsonOpts));
                Log.Info($"Wrote default station list: {path}");
                return Defaults;
            }

            var stations = JsonSerializer.Deserialize<RadioStation[]>(File.ReadAllText(path));
            if (stations is null || stations.Length == 0)
            {
                Log.Warn("stations.json empty/invalid — using defaults");
                return Defaults;
            }
            Log.Info($"Loaded {stations.Length} stations from {path}");
            return stations;
        }
        catch (Exception ex)
        {
            Log.Error($"Loading stations failed ({ex.Message}) — using defaults");
            return Defaults;
        }
    }

    public static void Save(IReadOnlyList<RadioStation> stations)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(stations, JsonOpts));
            Log.Info($"Saved {stations.Count} stations to {ConfigPath}");
        }
        catch (Exception ex)
        {
            Log.Error($"Saving stations failed: {ex.Message}");
        }
    }
}
