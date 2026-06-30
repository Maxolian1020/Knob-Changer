using Microsoft.Win32;
using VolumeRedirector.Services.Interfaces;

namespace VolumeRedirector.Services;

public sealed class StartupManager : IStartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "VolumeRedirector";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) is not null;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true) ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
        {
            key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }
}
