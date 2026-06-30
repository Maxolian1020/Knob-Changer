using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using VolumeRedirector.Services;
using VolumeRedirector.Services.Interfaces;

namespace VolumeRedirector;

public sealed class ApplicationHost : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LoggingService _loggingService;
    private readonly IKeyboardHookService _keyboardHookService;
    private readonly IAudioManager _audioManager;
    private readonly ITrayManager _trayManager;
    private readonly ISettingsManager _settingsManager;
    private readonly System.Threading.Timer _refreshTimer;
    private readonly System.Threading.Timer _volumeFlushTimer;
    private readonly object _volumeSync = new();
    private int _pendingVolumeDelta;
    private bool _volumeFlushScheduled;

    public ApplicationHost()
    {
        var services = new ServiceCollection();
        services.AddSingleton<LoggingService>();
        services.AddSingleton<ISettingsManager, SettingsManager>();
        services.AddSingleton<IAudioManager, AudioManager>();
        services.AddSingleton<IKeyboardHookService, KeyboardHookService>();
        services.AddSingleton<IStartupManager, StartupManager>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<ITrayManager, TrayManager>();

        _serviceProvider = services.BuildServiceProvider();
        _loggingService = _serviceProvider.GetRequiredService<LoggingService>();
        _settingsManager = _serviceProvider.GetRequiredService<ISettingsManager>();
        _audioManager = _serviceProvider.GetRequiredService<IAudioManager>();
        _keyboardHookService = _serviceProvider.GetRequiredService<IKeyboardHookService>();
        _trayManager = _serviceProvider.GetRequiredService<ITrayManager>();
        _refreshTimer = new System.Threading.Timer(_ => _ = _audioManager.RefreshAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        _volumeFlushTimer = new System.Threading.Timer(_ => FlushPendingVolumeChanges(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Start()
    {
        _settingsManager.Load();
        _keyboardHookService.MediaKeyPressed += HandleMediaKeyPressed;
        _keyboardHookService.Start();
        _trayManager.Initialize();

        var preferredDevice = !string.IsNullOrWhiteSpace(_settingsManager.Settings.SelectedDeviceName)
            ? _settingsManager.Settings.SelectedDeviceName
            : _settingsManager.Settings.SelectedDeviceId;

        if (!string.IsNullOrWhiteSpace(preferredDevice))
        {
            _ = _audioManager.SetSelectedDeviceAsync(preferredDevice);
        }

        _ = _audioManager.RefreshAsync();

        _loggingService.LogInformation("Application started.");
    }

    public void Dispose()
    {
        _keyboardHookService.MediaKeyPressed -= HandleMediaKeyPressed;
        _refreshTimer.Dispose();
        _volumeFlushTimer.Dispose();
        _keyboardHookService.Dispose();
        _audioManager.Dispose();
        _trayManager.Dispose();
        _loggingService.Dispose();
    }

    private async void HandleMediaKeyPressed(object? sender, int keyCode)
    {
        _loggingService.LogInformation($"Media key pressed: {keyCode}");
        switch (keyCode)
        {
            case 0xAF:
                QueueVolumeAdjustment(1);
                break;
            case 0xAE:
                QueueVolumeAdjustment(-1);
                break;
            case 0xAD:
                await _audioManager.ToggleMuteAsync();
                var selectedDevice = _audioManager.GetSelectedDevice();
                if (selectedDevice is not null)
                {
                    var displayText = selectedDevice.IsMuted ? "Muted" : $"{Math.Round(selectedDevice.Volume * 100)}%";
                    _trayManager.ShowNotification("Volume", displayText, false);
                }
                break;
        }
    }

    private void QueueVolumeAdjustment(int delta)
    {
        lock (_volumeSync)
        {
            _pendingVolumeDelta += delta;
            if (_volumeFlushScheduled)
            {
                return;
            }

            _volumeFlushScheduled = true;
        }

        _volumeFlushTimer.Change(TimeSpan.FromMilliseconds(60), Timeout.InfiniteTimeSpan);
    }

    private void FlushPendingVolumeChanges()
    {
        int delta;
        lock (_volumeSync)
        {
            delta = _pendingVolumeDelta;
            _pendingVolumeDelta = 0;
            _volumeFlushScheduled = false;
        }

        if (delta == 0)
        {
            return;
        }

        var amount = Math.Abs(delta) * _settingsManager.Settings.VolumeStep;
        if (delta > 0)
        {
            _audioManager.IncreaseVolumeAsync(amount).GetAwaiter().GetResult();
        }
        else
        {
            _audioManager.DecreaseVolumeAsync(amount).GetAwaiter().GetResult();
        }

        var selectedDevice = _audioManager.GetSelectedDevice();
        if (selectedDevice is not null)
        {
            var displayText = selectedDevice.IsMuted ? "Muted" : $"{Math.Round(selectedDevice.Volume * 100)}%";
            _trayManager.ShowNotification("Volume", displayText, false);
        }
    }
}
