using System.IO;
using System.Text.Json;
using VolumeRedirector.Models;
using VolumeRedirector.Services.Interfaces;

namespace VolumeRedirector.Services;

public sealed class SettingsManager : ISettingsManager
{
    private readonly string _settingsPath;

    public SettingsManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appData, "VolumeRedirector");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
        Settings = new AppSettings();
    }

    public AppSettings Settings { get; }

    public void Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (settings is not null)
            {
                Settings.SelectedDeviceId = settings.SelectedDeviceId;
                Settings.SelectedDeviceName = settings.SelectedDeviceName;
                Settings.VolumeStep = settings.VolumeStep > 0 ? settings.VolumeStep : 0.02;
                Settings.StartWithWindows = settings.StartWithWindows;
            }
        }
        catch (Exception)
        {
            // Ignore malformed settings and fall back to defaults.
        }
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }
}
