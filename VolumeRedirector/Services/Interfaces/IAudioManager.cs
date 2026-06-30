using VolumeRedirector.Models;

namespace VolumeRedirector.Services.Interfaces;

public interface IAudioManager : IDisposable
{
    event EventHandler<PlaybackDeviceInfo?>? SelectedDeviceChanged;
    event EventHandler<string>? DeviceUnavailable;

    IReadOnlyList<PlaybackDeviceInfo> GetPlaybackDevices();
    PlaybackDeviceInfo? GetSelectedDevice();
    Task SetSelectedDeviceAsync(string deviceId);
    Task SetVolumeAsync(double volume);
    Task IncreaseVolumeAsync(double step);
    Task DecreaseVolumeAsync(double step);
    Task ToggleMuteAsync();
    Task RefreshAsync();
}
