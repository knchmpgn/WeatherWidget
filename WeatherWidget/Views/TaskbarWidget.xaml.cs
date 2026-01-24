using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using WeatherWidget.Models;
using WeatherWidget.Services;

namespace WeatherWidget.Views
{
    public partial class TaskbarWidget : Window
    {
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(IntPtr h1, IntPtr h2, string c, string? n);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("shell32.dll")]
        private static extern IntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uCallbackMessage;
            public int uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        private static IntPtr GetWindowLong(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            else
                return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        // AppBar messages
        private const int ABM_GETTASKBARPOS = 5;
        private const int ABE_LEFT = 0;
        private const int ABE_TOP = 1;
        private const int ABE_RIGHT = 2;
        private const int ABE_BOTTOM = 3;

        // Window positioning
        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new(-2);
        private static readonly IntPtr HWND_BOTTOM = new(1);

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_SHOWNOACTIVATE = 4;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private readonly WeatherService _ws = new();
        private readonly LocationService _ls = new();
        private WeatherData? _data;
        private LocationData? _loc;

        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _zOrderTimer;
        private readonly DispatcherTimer _themeWatchTimer;
        private readonly DispatcherTimer _visibilityTimer;

        private WeatherFlyout? _currentFlyout;
        private bool _isHovering;
        private bool _isTaskbarDark = true;
        private int _taskbarEdge = ABE_BOTTOM;

        public TaskbarWidget()
        {
            InitializeComponent();

            this.Opacity = 0;
            this.Background = Brushes.Transparent;
            this.Left = -10000;
            this.Top = -10000;

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _refreshTimer.Tick += async (s, e) => await LoadData();
            _refreshTimer.Start();

            _zOrderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _zOrderTimer.Tick += (s, e) => EnsureWidgetOnTop();
            _zOrderTimer.Start();

            _themeWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _themeWatchTimer.Tick += (s, e) => UpdateTextColor();
            _themeWatchTimer.Start();

            // New timer to aggressively keep widget visible
            _visibilityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _visibilityTimer.Tick += (s, e) => ForceWidgetVisible();
            _visibilityTimer.Start();

            UpdateTextColor();
        }

        private void ForceWidgetVisible()
        {
            if (!IsLoaded || _data == null) return;

            // Aggressively keep widget visible
            if (this.Visibility != Visibility.Visible)
            {
                this.Visibility = Visibility.Visible;
            }

            if (this.Opacity < 1.0 && _currentFlyout == null)
            {
                this.Opacity = 1.0;
            }

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            }
        }

        private void UpdateTextColor()
        {
            bool isDark = IsTaskbarDark();
            if (isDark != _isTaskbarDark)
            {
                _isTaskbarDark = isDark;
                Color textColor = _isTaskbarDark ? Colors.White : Color.FromRgb(32, 32, 32);

                TempText.Foreground = new SolidColorBrush(textColor);
                ConditionText.Foreground = new SolidColorBrush(textColor);
            }
        }

        private static bool IsTaskbarDark()
        {
            try
            {
                var useLightTheme = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "SystemUsesLightTheme", 1);
                return useLightTheme?.ToString() == "0";
            }
            catch
            {
                return true;
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // Set window as tool window to prevent taskbar button
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE).ToInt32();
            _ = SetWindowLong(hwnd, GWL_EXSTYLE, new IntPtr(exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));

            // Disable window shadow for cleaner taskbar integration
            int disableShadow = 2;
            _ = DwmSetWindowAttribute(hwnd, 2, ref disableShadow, sizeof(int));

            // Detect taskbar position
            DetectTaskbarPosition();

            await LoadData();
        }

        private void DetectTaskbarPosition()
        {
            APPBARDATA abd = new APPBARDATA();
            abd.cbSize = Marshal.SizeOf(abd);
            IntPtr result = SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);

            if (result != IntPtr.Zero)
            {
                _taskbarEdge = abd.uEdge;
            }
            else
            {
                _taskbarEdge = ABE_BOTTOM;
            }
        }

        private async Task LoadData()
        {
            _loc ??= await _ls.GetCurrentLocationAsync();
            _data = await _ws.GetWeatherDataAsync(_loc.Latitude, _loc.Longitude);

            if (_data != null)
            {
                TempText.Text = _data.Temperature.ToString("N0") + "°";
                ConditionText.Text = _data.Condition;
                WeatherIcon.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/PNG/{_data.IconCode}.png"));

                this.UpdateLayout();
                PositionWidget();

                var fadeAnim = new DoubleAnimation(1, TimeSpan.FromMilliseconds(300));
                this.BeginAnimation(OpacityProperty, fadeAnim);

                EnsureWidgetOnTop();
            }
        }

        private void PositionWidget()
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            IntPtr tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);

            if (tray != IntPtr.Zero && GetWindowRect(tray, out RECT trayRect) && GetWindowRect(taskbar, out RECT tbRect))
            {
                var dpi = VisualTreeHelper.GetDpi(this);

                switch (_taskbarEdge)
                {
                    case ABE_BOTTOM:
                        PositionForBottomTaskbar(trayRect, tbRect, dpi);
                        break;
                    case ABE_TOP:
                        PositionForTopTaskbar(trayRect, tbRect, dpi);
                        break;
                    case ABE_LEFT:
                        PositionForLeftTaskbar(trayRect, tbRect, dpi);
                        break;
                    case ABE_RIGHT:
                        PositionForRightTaskbar(trayRect, tbRect, dpi);
                        break;
                }
            }
        }

        private void PositionForBottomTaskbar(RECT trayRect, RECT tbRect, DpiScale dpi)
        {
            double tbHeight = (tbRect.Bottom - tbRect.Top) / dpi.DpiScaleY;
            double trayLeft = trayRect.Left / dpi.DpiScaleX;

            this.Left = trayLeft - this.ActualWidth - 16;
            this.Top = (tbRect.Top / dpi.DpiScaleY) + ((tbHeight - this.ActualHeight) / 2);
        }

        private void PositionForTopTaskbar(RECT trayRect, RECT tbRect, DpiScale dpi)
        {
            double tbHeight = (tbRect.Bottom - tbRect.Top) / dpi.DpiScaleY;
            double trayLeft = trayRect.Left / dpi.DpiScaleX;

            this.Left = trayLeft - this.ActualWidth - 16;
            this.Top = (tbRect.Top / dpi.DpiScaleY) + ((tbHeight - this.ActualHeight) / 2);
        }

        private void PositionForLeftTaskbar(RECT trayRect, RECT tbRect, DpiScale dpi)
        {
            double tbWidth = (tbRect.Right - tbRect.Left) / dpi.DpiScaleX;
            double trayTop = trayRect.Top / dpi.DpiScaleY;

            this.Left = (tbRect.Left / dpi.DpiScaleX) + ((tbWidth - this.ActualWidth) / 2);
            this.Top = trayTop - this.ActualHeight - 16;
        }

        private void PositionForRightTaskbar(RECT trayRect, RECT tbRect, DpiScale dpi)
        {
            double tbWidth = (tbRect.Right - tbRect.Left) / dpi.DpiScaleX;
            double trayTop = trayRect.Top / dpi.DpiScaleY;

            this.Left = (tbRect.Left / dpi.DpiScaleX) + ((tbWidth - this.ActualWidth) / 2);
            this.Top = trayTop - this.ActualHeight - 16;
        }

        private void EnsureWidgetOnTop()
        {
            if (!IsLoaded || _data == null) return;

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (_currentFlyout != null && _currentFlyout.IsVisible)
            {
                _ = SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            else if (!_isHovering)
            {
                _ = SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        private void Widget_MouseEnter(object sender, MouseEventArgs e)
        {
            _isHovering = true;
            AnimateBackground(true);
        }

        private void Widget_MouseLeave(object sender, MouseEventArgs e)
        {
            _isHovering = false;
            AnimateBackground(false);
        }

        private void AnimateBackground(bool show)
        {
            var targetColor = show ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(0, 255, 255, 255);
            var animation = new ColorAnimation(targetColor, TimeSpan.FromMilliseconds(150));

            if (MainBorder.Background is not SolidColorBrush brush || brush.IsFrozen)
            {
                brush = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                MainBorder.Background = brush;
            }

            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private void Widget_Click(object sender, MouseButtonEventArgs e)
        {
            if (_data == null || _loc == null) return;

            if (_currentFlyout?.IsVisible == true)
            {
                _currentFlyout.Close();
                return;
            }

            Point topLeftDevice = this.PointToScreen(new Point(0, 0));
            var dpi = VisualTreeHelper.GetDpi(this);

            double left = topLeftDevice.X / dpi.DpiScaleX;
            double top = topLeftDevice.Y / dpi.DpiScaleY;

            var anchorRect = new Rect(left, top, this.ActualWidth, this.ActualHeight);

            _currentFlyout = new WeatherFlyout(_data, _loc, anchorRect);
            _currentFlyout.Closed += Flyout_Closed;

            // Don't set Topmost on flyout - let it be a normal window
            _currentFlyout.Show();
            _currentFlyout.Activate();

            // Widget stays visible
            EnsureWidgetOnTop();
        }

        private void Flyout_Closed(object? sender, EventArgs e)
        {
            _currentFlyout = null;

            this.Dispatcher.Invoke(() =>
            {
                _loc = null;
                Dispatcher.BeginInvoke(new Action(async () => await LoadData()));
                EnsureWidgetOnTop();
            }, DispatcherPriority.Send);
        }

        private void Widget_RightClick(object sender, MouseButtonEventArgs e)
        {
            var menu = new System.Windows.Controls.ContextMenu();

            var settings = new System.Windows.Controls.MenuItem { Header = "Settings" };
            settings.Click += (s, a) => OpenSettings();
            menu.Items.Add(settings);

            menu.Items.Add(new System.Windows.Controls.Separator());

            var exit = new System.Windows.Controls.MenuItem { Header = "Exit WeatherWidget" };
            exit.Click += (s, a) => Application.Current.Shutdown();
            menu.Items.Add(exit);

            menu.IsOpen = true;
        }

        private void OpenSettings()
        {
            if (_data != null && _loc != null)
            {
                Point topLeftDevice = this.PointToScreen(new Point(0, 0));
                var dpi = VisualTreeHelper.GetDpi(this);

                double left = topLeftDevice.X / dpi.DpiScaleX;
                double top = topLeftDevice.Y / dpi.DpiScaleY;

                var anchorRect = new Rect(left, top, this.ActualWidth, this.ActualHeight);

                _currentFlyout = new WeatherFlyout(_data, _loc, anchorRect);
                _currentFlyout.Closed += Flyout_Closed;
                _currentFlyout.Show();
                _currentFlyout.Activate();

                EnsureWidgetOnTop();
            }
        }

        internal enum AccentState { ACCENT_ENABLE_BLURBEHIND = 3 }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }
    }
}