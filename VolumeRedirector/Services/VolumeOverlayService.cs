using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace VolumeRedirector.Services;

public interface IVolumeOverlayService : IDisposable
{
    void Show(double volumePercent, bool isMuted);
}

public sealed class VolumeOverlayService : IVolumeOverlayService, IDisposable
{
    private const int OverlayWidth = 180;
    private const int OverlayHeight = 46;
    private const int SwpNoActivate = 0x0010;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoSize = 0x0001;
    private const int SwpShowWindow = 0x0040;
    private const int SwpNoZOrder = 0x0004;
    private const int HwndTopmost = -1;
    private const int ShowWindowShow = 5;

    private OverlayWindow? _overlayWindow;
    private System.Windows.Forms.Timer? _hideTimer;
    private bool _isAvailable = true;
    private bool _isInitialized;

    public void Show(double volumePercent, bool isMuted)
    {
        if (!_isAvailable)
        {
            return;
        }

        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => Show(volumePercent, isMuted)));
            return;
        }

        EnsureWindowInitialized();
        if (_overlayWindow is null || _hideTimer is null)
        {
            return;
        }

        try
        {
            var clippedPercent = Math.Clamp((int)Math.Round(volumePercent), 0, 100);
            _overlayWindow.SetText(isMuted ? "Muted" : $"{clippedPercent}");
            _overlayWindow.SetValue(clippedPercent, isMuted);
            _overlayWindow.PositionAtBottomCenter();
            _overlayWindow.ShowOverlay();

            _hideTimer.Stop();
            _hideTimer.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            _isAvailable = false;
        }
    }

    private void Hide()
    {
        if (!_isAvailable || _overlayWindow is null || _hideTimer is null)
        {
            return;
        }

        try
        {
            _hideTimer.Stop();
            _overlayWindow.HideOverlay();
        }
        catch
        {
            _overlayWindow.HideOverlay();
        }
    }

    public void Dispose()
    {
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(Dispose));
            return;
        }

        _hideTimer?.Stop();
        _overlayWindow?.Close();
    }

    private void EnsureWindowInitialized()
    {
        if (_isInitialized || !_isAvailable)
        {
            return;
        }

        try
        {
            _overlayWindow = new OverlayWindow(OverlayWidth, OverlayHeight);
            _hideTimer = new System.Windows.Forms.Timer { Interval = 900 };
            _hideTimer.Tick += (_, _) => Hide();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            _isAvailable = false;
            _overlayWindow = null;
            _hideTimer = null;
        }
    }

    private sealed class OverlayWindow : Window
    {
        private const int GwlExstyle = -20;
        private const int WsExAppwindow = 0x00040000;
        private const int WsExToolwindow = 0x00000080;
        private const int WsExTopmost = 0x00000008;
        private const int WsExLayered = 0x00080000;
        private const int WsExNoactivate = 0x08000000;
        private const int WsExTransparent = 0x00000020;
        private const int SwpFrameChanged = 0x0020;

        private readonly TextBlock _statusText;
        private readonly System.Windows.Controls.ProgressBar _volumeBar;

        public OverlayWindow(int width, int height)
        {
            Width = width;
            Height = height;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Opacity = 1.0;
            IsHitTestVisible = false;

            var shell = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 20, 20, 20)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 6, 8, 6),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 255, 255, 255))
            };

            var layout = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(0)
            };

            _statusText = new TextBlock
            {
                Text = "0",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                TextAlignment = System.Windows.TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                MinWidth = 24
            };

            _volumeBar = new System.Windows.Controls.ProgressBar
            {
                Height = 6,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Foreground = System.Windows.Media.Brushes.White,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 255, 255, 255)),
                Margin = new Thickness(0),
                Width = 108,
                VerticalAlignment = VerticalAlignment.Center
            };

            layout.Children.Add(_statusText);
            layout.Children.Add(_volumeBar);
            shell.Child = layout;
            Content = shell;

            SourceInitialized += (_, _) => ApplyWindowStyle();
        }

        public void SetText(string text) => _statusText.Text = text;

        public void SetValue(int percent, bool isMuted)
        {
            _volumeBar.Value = Math.Clamp(percent, 0, 100);
            _volumeBar.Foreground = isMuted ? System.Windows.Media.Brushes.OrangeRed : System.Windows.Media.Brushes.White;
        }

        public void PositionAtBottomCenter()
        {
            var workingArea = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? System.Windows.Forms.Screen.GetWorkingArea(System.Drawing.Point.Empty);
            Left = workingArea.Left + ((workingArea.Width - Width) / 2);
            Top = workingArea.Bottom - Height - 16;
        }

        public void ShowOverlay()
        {
            if (Visibility != Visibility.Visible)
            {
                Show();
            }

            Opacity = 0.0;
            Margin = new Thickness(0, 0, 0, 8);
            IsHitTestVisible = false;
            Topmost = true;
            UpdateLayout();

            var animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, animation);

            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                ShowWindow(helper.Handle, 8);
                SetWindowPos(helper.Handle, new IntPtr(HwndTopmost), 0, 0, 0, 0, SwpNoActivate | SwpShowWindow | SwpNoMove | SwpNoSize | SwpNoZOrder);
            }
        }

        public void HideOverlay()
        {
            var animation = new DoubleAnimation
            {
                From = Opacity,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = EasingMode.EaseIn }
            };
            BeginAnimation(OpacityProperty, animation);
            IsHitTestVisible = false;
            Topmost = true;
        }

        private void ApplyWindowStyle()
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var hwnd = helper.Handle;
            var exStyle = GetWindowLongPtr(hwnd, GwlExstyle);
            exStyle &= ~WsExAppwindow;
            exStyle |= WsExToolwindow | WsExTopmost | WsExLayered | WsExNoactivate | WsExTransparent;
            SetWindowLongPtr(hwnd, GwlExstyle, exStyle);
            SetWindowPos(hwnd, new IntPtr(HwndTopmost), 0, 0, 0, 0, SwpNoActivate | SwpShowWindow | SwpNoMove | SwpNoSize | SwpNoZOrder);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
