namespace VolumeRedirector.Models;

public sealed class PlaybackDeviceInfo
{
    public required string Id { get; init; }
    public required string FriendlyName { get; init; }
    public double Volume { get; init; }
    public bool IsMuted { get; init; }
    public bool IsDefault { get; init; }
    public bool IsSelected { get; init; }
    public bool HasIcon { get; init; }
}
