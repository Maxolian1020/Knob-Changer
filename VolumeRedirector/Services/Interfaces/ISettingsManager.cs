using VolumeRedirector.Models;

namespace VolumeRedirector.Services.Interfaces;

public interface ISettingsManager
{
    AppSettings Settings { get; }
    void Load();
    void Save();
}
