namespace VolumeRedirector.Services.Interfaces;

public interface ITrayManager : IDisposable
{
    void Initialize();
    void ShowNotification(string title, string message, bool showBalloon = true);
}
