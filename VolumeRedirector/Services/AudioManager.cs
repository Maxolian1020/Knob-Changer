using NAudio.CoreAudioApi;
using VolumeRedirector.Models;
using VolumeRedirector.Services.Interfaces;

namespace VolumeRedirector.Services;

public sealed class AudioManager : IAudioManager
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly object _sync = new();
    private string? _selectedDeviceId;

    public event EventHandler<PlaybackDeviceInfo?>? SelectedDeviceChanged;
    public event EventHandler<string>? DeviceUnavailable;

    public IReadOnlyList<PlaybackDeviceInfo> GetPlaybackDevices()
    {
        lock (_sync)
        {
            try
            {
                var selectedDeviceId = _selectedDeviceId;
                var devices = EnumerateRenderDevices()
                    .Select(device => ToPlaybackDeviceInfo(device, selectedDeviceId))
                    .Where(device => device is not null)
                    .Cast<PlaybackDeviceInfo>()
                    .OrderBy(device => device.FriendlyName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return devices;
            }
            catch
            {
                return Array.Empty<PlaybackDeviceInfo>();
            }
        }
    }

    public PlaybackDeviceInfo? GetSelectedDevice()
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(_selectedDeviceId))
            {
                return null;
            }

            try
            {
                var device = TryGetDeviceById(_selectedDeviceId);
                return device is null ? null : ToPlaybackDeviceInfo(device, _selectedDeviceId);
            }
            catch
            {
                return null;
            }
        }
    }

    public async Task SetSelectedDeviceAsync(string deviceId)
    {
        await Task.Run(() =>
        {
            lock (_sync)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(deviceId))
                    {
                        _selectedDeviceId = null;
                        SelectedDeviceChanged?.Invoke(this, null);
                        return;
                    }

                    var device = ResolveDevice(deviceId);
                    if (device is null)
                    {
                        _selectedDeviceId = null;
                        SelectedDeviceChanged?.Invoke(this, null);
                        return;
                    }

                    _selectedDeviceId = device.ID;
                    SelectedDeviceChanged?.Invoke(this, GetSelectedDevice());
                }
                catch
                {
                    _selectedDeviceId = null;
                    SelectedDeviceChanged?.Invoke(this, null);
                }
            }
        });
    }

    public async Task SetVolumeAsync(double volume)
    {
        var clamped = Math.Clamp(volume, 0.0, 1.0);
        ApplyToSelectedDevice(endpointVolume =>
        {
            endpointVolume.Mute = false;
            endpointVolume.MasterVolumeLevelScalar = (float)clamped;
        });
        await Task.CompletedTask;
    }

    public async Task IncreaseVolumeAsync(double step)
    {
        ApplyToSelectedDevice(endpointVolume =>
        {
            var current = endpointVolume.MasterVolumeLevelScalar;
            var next = Math.Clamp(current + (float)step, 0f, 1f);
            endpointVolume.Mute = false;
            endpointVolume.MasterVolumeLevelScalar = next;
            SelectedDeviceChanged?.Invoke(this, GetSelectedDevice());
        });

        await Task.CompletedTask;
    }

    public async Task DecreaseVolumeAsync(double step)
    {
        ApplyToSelectedDevice(endpointVolume =>
        {
            var current = endpointVolume.MasterVolumeLevelScalar;
            var next = Math.Clamp(current - (float)step, 0f, 1f);
            endpointVolume.Mute = false;
            endpointVolume.MasterVolumeLevelScalar = next;
            SelectedDeviceChanged?.Invoke(this, GetSelectedDevice());
        });

        await Task.CompletedTask;
    }

    public async Task ToggleMuteAsync()
    {
        ApplyToSelectedDevice(endpointVolume =>
        {
            endpointVolume.Mute = !endpointVolume.Mute;
            SelectedDeviceChanged?.Invoke(this, GetSelectedDevice());
        });

        await Task.CompletedTask;
    }

    public Task RefreshAsync()
    {
        lock (_sync)
        {
            try
            {
                var currentDeviceId = _selectedDeviceId;
                var availableDevices = EnumerateRenderDevices();
                var stillAvailable = !string.IsNullOrWhiteSpace(currentDeviceId)
                    && availableDevices.Any(device => device.ID == currentDeviceId);

                if (!stillAvailable && !string.IsNullOrWhiteSpace(currentDeviceId))
                {
                    DeviceUnavailable?.Invoke(this, currentDeviceId);
                }

                if (!string.IsNullOrWhiteSpace(currentDeviceId) && availableDevices.Any(device => device.ID == currentDeviceId))
                {
                    SelectedDeviceChanged?.Invoke(this, GetSelectedDevice());
                }
            }
            catch
            {
                SelectedDeviceChanged?.Invoke(this, null);
            }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _enumerator.Dispose();
    }

    private void ApplyToSelectedDevice(Action<AudioEndpointVolume> action)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(_selectedDeviceId))
            {
                return;
            }

            try
            {
                var device = TryGetDeviceById(_selectedDeviceId);
                var endpointVolume = TryGetEndpointVolume(device);
                if (endpointVolume is null)
                {
                    return;
                }

                action(endpointVolume);
            }
            catch
            {
                // Ignore unsupported endpoints.
            }
        }
    }

    private MMDevice? TryGetDeviceById(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        try
        {
            return EnumerateRenderDevices().FirstOrDefault(device => device.ID == deviceId);
        }
        catch
        {
            return null;
        }
    }

    private MMDevice? ResolveDevice(string preferredDevice)
    {
        if (string.IsNullOrWhiteSpace(preferredDevice))
        {
            return null;
        }

        var devices = EnumerateRenderDevices();
        return devices.FirstOrDefault(device =>
            string.Equals(device.ID, preferredDevice, StringComparison.OrdinalIgnoreCase)
            || string.Equals(device.FriendlyName, preferredDevice, StringComparison.OrdinalIgnoreCase));
    }

    private AudioEndpointVolume? TryGetEndpointVolume(MMDevice? device)
    {
        if (device is null)
        {
            return null;
        }

        try
        {
            return device.AudioEndpointVolume;
        }
        catch
        {
            return null;
        }
    }

    private IReadOnlyList<MMDevice> EnumerateRenderDevices()
    {
        try
        {
            return _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
        }
        catch
        {
            return Array.Empty<MMDevice>();
        }
    }

    private PlaybackDeviceInfo? ToPlaybackDeviceInfo(MMDevice device, string? selectedDeviceId)
    {
        try
        {
            var deviceId = SafeRead(() => device.ID, string.Empty);
            var friendlyName = SafeRead(() => device.FriendlyName, "Unknown device");
            var endpointVolume = TryGetEndpointVolume(device);
            var volume = endpointVolume?.MasterVolumeLevelScalar ?? 0f;
            var defaultDeviceId = SafeRead(() => _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID, string.Empty);

            return new PlaybackDeviceInfo
            {
                Id = deviceId,
                FriendlyName = friendlyName,
                Volume = Math.Round(volume, 3),
                IsMuted = endpointVolume?.Mute ?? false,
                IsDefault = !string.IsNullOrWhiteSpace(defaultDeviceId) && deviceId == defaultDeviceId,
                IsSelected = !string.IsNullOrWhiteSpace(selectedDeviceId) && deviceId == selectedDeviceId,
                HasIcon = true
            };
        }
        catch
        {
            return new PlaybackDeviceInfo
            {
                Id = SafeRead(() => device.ID, string.Empty),
                FriendlyName = SafeRead(() => device.FriendlyName, "Unknown device"),
                Volume = 0,
                IsMuted = false,
                IsDefault = false,
                IsSelected = false,
                HasIcon = true
            };
        }
    }

    private static T SafeRead<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch
        {
            return fallback;
        }
    }
}
