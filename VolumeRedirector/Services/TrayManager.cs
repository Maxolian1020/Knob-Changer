using System.Drawing;
using System.Windows.Forms;
using VolumeRedirector.Services.Interfaces;

namespace VolumeRedirector.Services;

public sealed class TrayManager : ITrayManager
{
    private readonly IAudioManager _audioManager;
    private readonly ISettingsManager _settingsManager;
    private readonly IStartupManager _startupManager;
    private readonly INotificationService _notificationService;
    private readonly LoggingService _loggingService;
    private readonly IVolumeOverlayService _volumeOverlayService;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _menuRefreshTimer;
    private bool _initialized;

    public TrayManager(IAudioManager audioManager, ISettingsManager settingsManager, IStartupManager startupManager, INotificationService notificationService, LoggingService loggingService)
    {
        _audioManager = audioManager;
        _settingsManager = settingsManager;
        _startupManager = startupManager;
        _notificationService = notificationService;
        _loggingService = loggingService;
        _volumeOverlayService = new VolumeOverlayService();

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Volume Redirector"
        };
        _menuRefreshTimer = new System.Windows.Forms.Timer { Interval = 80 };
        _menuRefreshTimer.Tick += (_, _) => RefreshMenu();
        _audioManager.SelectedDeviceChanged += (_, _) => ScheduleMenuRefresh();
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        RefreshMenu();
        _ = _audioManager.RefreshAsync();
    }

    public void ShowNotification(string title, string message, bool showBalloon = true)
    {
        try
        {
            if (showBalloon)
            {
                _notifyIcon.ShowBalloonTip(1500, title, message, ToolTipIcon.Info);
            }

            if (title == "Volume")
            {
                _volumeOverlayService.Show(ParseVolumePercent(message), message.Contains("Muted", StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogInformation($"Notification failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _volumeOverlayService.Dispose();
        _notifyIcon.Dispose();
    }

    private void ScheduleMenuRefresh()
    {
        _menuRefreshTimer.Stop();
        _menuRefreshTimer.Start();
    }

    private void RefreshMenu()
    {
        if (!_initialized)
        {
            return;
        }

        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(RefreshMenu);
            return;
        }

        var menu = new ContextMenuStrip();
        var playbackDevices = _audioManager.GetPlaybackDevices();
        var currentDevice = _audioManager.GetSelectedDevice();
        var matchingDeviceId = currentDevice?.Id;

        if (currentDevice is null)
        {
            var preferredDevice = !string.IsNullOrWhiteSpace(_settingsManager.Settings.SelectedDeviceName)
                ? _settingsManager.Settings.SelectedDeviceName
                : _settingsManager.Settings.SelectedDeviceId;

            if (!string.IsNullOrWhiteSpace(preferredDevice))
            {
                currentDevice = playbackDevices.FirstOrDefault(device =>
                    string.Equals(device.Id, preferredDevice, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(device.FriendlyName, preferredDevice, StringComparison.OrdinalIgnoreCase));
                matchingDeviceId = currentDevice?.Id;
            }
        }

        var currentName = currentDevice?.FriendlyName ?? "none";
        var currentVolume = currentDevice is null ? 0 : Math.Round(currentDevice.Volume * 100);
        var currentItem = new ToolStripMenuItem(currentDevice is null ? "Current Device: none" : $"Current Device: {currentName} ({currentVolume}%)")
        {
            Enabled = false
        };
        menu.Items.Add(currentItem);
        menu.Items.Add(new ToolStripSeparator());

        var deviceHeader = new ToolStripMenuItem("Select Device") { Enabled = false };
        menu.Items.Add(deviceHeader);

        foreach (var device in playbackDevices)
        {
            var isCurrentDevice = string.Equals(device.Id, matchingDeviceId, StringComparison.OrdinalIgnoreCase)
                || (currentDevice is not null && string.Equals(device.FriendlyName, currentDevice.FriendlyName, StringComparison.OrdinalIgnoreCase));
            var item = new ToolStripMenuItem((isCurrentDevice ? "✔ " : string.Empty) + device.FriendlyName)
            {
                Tag = device.Id
            };
            item.Click += (_, _) => SelectDevice(device.Id);
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());
        var volumeHeader = new ToolStripMenuItem("Volume Step") { Enabled = false };
        menu.Items.Add(volumeHeader);
        foreach (var step in new[] { 0.01, 0.02, 0.05, 0.1 })
        {
            var item = new ToolStripMenuItem(FormatStep(step))
            {
                Checked = Math.Abs(step - _settingsManager.Settings.VolumeStep) < 0.0001,
                CheckOnClick = false
            };
            item.Click += (_, _) => SetVolumeStep(step);
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());
        var startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = _startupManager.IsEnabled
        };
        startupItem.Click += (_, _) => ToggleStartup(startupItem);
        menu.Items.Add(startupItem);

        menu.Items.Add(new ToolStripSeparator());
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => System.Windows.Application.Current?.Shutdown();
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;
    }

    private void SetVolumeStep(double step)
    {
        _settingsManager.Settings.VolumeStep = step;
        _settingsManager.Save();
        _loggingService.LogInformation($"Volume step set to {step:P0}");
        ShowNotification("Volume Step", FormatStep(step));
        RefreshMenu();
    }

    private void ToggleStartup(ToolStripMenuItem item)
    {
        _startupManager.SetEnabled(!item.Checked);
        _settingsManager.Settings.StartWithWindows = _startupManager.IsEnabled;
        _settingsManager.Save();
        item.Checked = _startupManager.IsEnabled;
        ShowNotification("Startup", _startupManager.IsEnabled ? "Volume Redirector will start with Windows." : "Startup entry removed.");
    }

    private void SelectDevice(string deviceId)
    {
        _settingsManager.Settings.SelectedDeviceId = deviceId;
        _settingsManager.Settings.SelectedDeviceName = _audioManager.GetPlaybackDevices().FirstOrDefault(device => device.Id == deviceId)?.FriendlyName;
        _settingsManager.Save();
        _ = _audioManager.SetSelectedDeviceAsync(deviceId);
        RefreshMenu();
        ShowNotification("Now controlling", _audioManager.GetSelectedDevice()?.FriendlyName ?? deviceId);
    }

    private void ShowCurrentDeviceInfo()
    {
        var device = _audioManager.GetSelectedDevice();
        if (device is null)
        {
            ShowNotification("Current Device", "No playback device selected.");
            return;
        }

        ShowNotification("Current Device", $"{device.FriendlyName}\n{Math.Round(device.Volume * 100)}% volume");
    }

    private static double ParseVolumePercent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return 0;
        }

        var candidate = message;
        var separatorIndex = candidate.IndexOf(':');
        if (separatorIndex >= 0)
        {
            candidate = candidate[(separatorIndex + 1)..];
        }

        candidate = candidate.Trim();
        if (candidate.StartsWith("Muted", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var percentIndex = candidate.IndexOf('%');
        if (percentIndex >= 0)
        {
            candidate = candidate[..percentIndex];
        }

        return double.TryParse(candidate, out var percent) ? percent : 0;
    }

    private static string FormatStep(double step) => $"{step * 100:0}%";
}
