using VolumeRedirector.Services.Interfaces;

namespace VolumeRedirector.Services;

public sealed class NotificationService : INotificationService
{
    public void Show(string title, string message)
    {
        try
        {
            var notification = new System.Windows.Forms.NotifyIcon
            {
                Visible = true,
                Icon = System.Drawing.SystemIcons.Application
            };

            notification.ShowBalloonTip(2000, title, message, System.Windows.Forms.ToolTipIcon.Info);
            notification.Dispose();
        }
        catch
        {
            // Ignore notification failures.
        }
    }
}
