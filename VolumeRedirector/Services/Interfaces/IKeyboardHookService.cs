namespace VolumeRedirector.Services.Interfaces;

public interface IKeyboardHookService : IDisposable
{
    event EventHandler<int>? MediaKeyPressed;
    void Start();
    void Stop();
}
