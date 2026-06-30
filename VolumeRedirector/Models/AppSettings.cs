namespace VolumeRedirector.Models;

public sealed class AppSettings
{
    public string? SelectedDeviceId { get; set; }
    public string? SelectedDeviceName { get; set; }
    public double VolumeStep { get; set; } = 0.02;
    public bool StartWithWindows { get; set; }
}
