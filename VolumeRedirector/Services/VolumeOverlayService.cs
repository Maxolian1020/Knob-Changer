using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace VolumeRedirector.Services;

public interface IVolumeOverlayService : IDisposable
{
    void Show(double volumePercent, bool isMuted);
}

public sealed class VolumeOverlayService : IVolumeOverlayService, IDisposable
{
    private const double OverlayWidth = 220;
    private const double OverlayHeight = 74;

    private readonly Window _window;
    private readonly DispatcherTimer _hideTimer;
    private readonly System.Windows.Controls.ProgressBar _volumeBar;
    private readonly TextBlock _statusText;

    public VolumeOverlayService()
    {
        _window = new Window
        {
            Width = OverlayWidth,
            Height = OverlayHeight,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Topmost = true,
            ShowActivated = false,
            IsHitTestVisible = false,
            Opacity = 0
        };

        var panel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 20, 20, 20)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 10, 12, 10),
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = OverlayWidth - 24
            }
        };

        _statusText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        };

        _volumeBar = new System.Windows.Controls.ProgressBar
        {
            Height = 6,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(0)
        };

        var container = (StackPanel)panel.Child;
        container.Children.Add(_statusText);
        container.Children.Add(_volumeBar);

        _window.Content = panel;

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _hideTimer.Tick += (_, _) => Hide();
    }

    public void Show(double volumePercent, bool isMuted)
    {
        try
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                return;
            }

            _ = dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var clippedPercent = Math.Clamp((int)Math.Round(volumePercent), 0, 100);
                    _statusText.Text = isMuted ? "Muted" : $"{clippedPercent}%";
                    _volumeBar.Value = clippedPercent;
                    _volumeBar.Foreground = isMuted ? new SolidColorBrush(Colors.OrangeRed) : Brushes.White;

                    PositionWindow();
                    if (!_window.IsVisible)
                    {
                        _window.Show();
                    }

                    _window.Visibility = Visibility.Visible;
                    _window.BeginAnimation(UIElement.OpacityProperty, null);
                    _window.Opacity = 1;
                    _hideTimer.Stop();
                    _hideTimer.Start();
                }
                catch
                {
                    // Ignore overlay failures.
                }
            });
        }
        catch
        {
            // Ignore overlay failures.
        }
    }

    private void Hide()
    {
        try
        {
            _hideTimer.Stop();
            _window.BeginAnimation(UIElement.OpacityProperty, null);
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
            _window.BeginAnimation(UIElement.OpacityProperty, fade);
        }
        catch
        {
            _window.Opacity = 0;
            _window.Visibility = Visibility.Hidden;
        }
    }

    public void Dispose()
    {
        _hideTimer.Stop();
        _window.Close();
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        _window.Left = workArea.Left + ((workArea.Width - OverlayWidth) / 2);
        _window.Top = workArea.Bottom - OverlayHeight - 18;
        _window.UpdateLayout();
    }
}
