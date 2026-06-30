namespace VolumeRedirector.Services.Interfaces;

public interface IStartupManager
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}
