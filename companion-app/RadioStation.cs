namespace MsfsMediaPlayer.Companion;

/// <summary>One internet-radio station. Stream URL should be a direct MP3/AAC HTTP stream.</summary>
internal sealed record RadioStation(string Name, string Url);
