using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using WeatherWidget.Models;
using WeatherWidget.Services;
using WinRT.Interop;
using Windows.Foundation;
using Windows.Graphics;

namespace WeatherWidget.Views
{
    public sealed partial class TaskbarWidget : Window
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(IntPtr h1, IntPtr h2, string? c, string? n);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("shell32.dll")]
        private static extern IntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

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

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        private static IntPtr GetWindowLong(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        private const int ABM_GETSTATE = 4;
        private const int ABM_GETTASKBARPOS = 5;
        private const int ABE_LEFT = 0;
        private const int ABE_TOP = 1;
        private const int ABE_RIGHT = 2;
        private const int ABE_BOTTOM = 3;
        private const int ABS_AUTOHIDE = 0x1;

        private static readonly IntPtr HWND_TOPMOST = new(-1);

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private readonly WeatherService _ws = new();
        private readonly LocationService _ls = new();
        private WeatherData? _data;
        private LocationData? _loc;

        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _zOrderTimer;
        private readonly DispatcherTimer _themeWatchTimer;
        private readonly DispatcherTimer _retryTimer;

        private WeatherFlyout? _currentFlyout;
        private bool _isHovering;
        private bool _isTaskbarDark = true;
        private int _taskbarEdge = ABE_BOTTOM;
        private AppWindow? _appWindow;
        private OverlappedPresenter? _presenter;

        public TaskbarWidget()
        {
            InitializeComponent();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _refreshTimer.Tick += async (s, e) => await LoadData();
            _refreshTimer.Start();

            _zOrderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _zOrderTimer.Tick += (s, e) => SyncWithTaskbarState();
            _zOrderTimer.Start();

            _themeWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _themeWatchTimer.Tick += (s, e) => UpdateTextColor();
            _themeWatchTimer.Start();

            _retryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _retryTimer.Tick += async (s, e) => await LoadData();

            UpdateTextColor();
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeWindow();
            DetectTaskbarPosition();
            _ = LoadData();
            UpdateWindowSize();
        }

        private void InitializeWindow()
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                _presenter = presenter;
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.IsAlwaysOnTop = true;
                presenter.SetBorderAndTitleBar(false, false);
            }

            if (_appWindow != null)
            {
                _appWindow.Title = "Weather";
                _appWindow.IsShownInSwitchers = false;
            }

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE).ToInt32();
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            exStyle &= ~WS_EX_APPWINDOW;
            _ = SetWindowLong(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
        }

        private async Task LoadData()
        {
            TempText.Text = "...";
            ConditionText.Text = "Locating...";
            if (_loc == null)
            {
                var locTask = _ls.GetCurrentLocationAsync();
                var completed = await Task.WhenAny(locTask, Task.Delay(TimeSpan.FromSeconds(5)));
                if (completed == locTask)
                {
                    _loc = await locTask;
                }
                else
                {
                    _loc = new LocationData { City = "Unknown", Latitude = 35.44, Longitude = -89.81 };
                    _ls.LastErrorMessage = "Location timeout";
                }
            }
            ConditionText.Text = "Fetching...";
            _data = await _ws.GetWeatherDataAsync(_loc.Latitude, _loc.Longitude);

            if (_data != null)
            {
                if (_retryTimer.IsEnabled)
                {
                    _retryTimer.Stop();
                }

                TempText.Text = _data.Temperature.ToString("N0") + "°";
                ConditionText.Text = _data.Condition;
                WeatherIcon.Source = new BitmapImage(new Uri($"ms-appx:///Assets/PNG/{_data.IconCode}.png"));

                PositionWidget();
                UpdateWindowSize();
                SyncWithTaskbarState();
                return;
            }

            TempText.Text = "--°";
            var details = _ws.LastErrorMessage ?? _ls.LastErrorMessage;
            ConditionText.Text = string.IsNullOrWhiteSpace(details) ? "Unable to load" : $"Error: {details}";
            WeatherIcon.Source = null;

            if (!_retryTimer.IsEnabled)
            {
                _retryTimer.Start();
            }
        }

        private void UpdateWindowSize()
        {
            if (_appWindow == null)
            {
                return;
            }

            RootGrid.UpdateLayout();
            double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
            int width = (int)Math.Ceiling(MainBorder.ActualWidth * scale);
            int height = (int)Math.Ceiling(MainBorder.ActualHeight * scale);
            if (width > 0 && height > 0)
            {
                _appWindow.Resize(new SizeInt32(width, height));
            }
        }

        private void UpdateTextColor()
        {
            bool isDark = IsTaskbarDark();
            if (isDark != _isTaskbarDark)
            {
                _isTaskbarDark = isDark;
                var textColor = _isTaskbarDark ? Colors.White : Windows.UI.Color.FromArgb(255, 32, 32, 32);

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

        private void Widget_Click(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(MainBorder).Properties.IsLeftButtonPressed == false)
            {
                return;
            }

            if (_data == null || _loc == null)
            {
                return;
            }

            if (_currentFlyout != null)
            {
                _currentFlyout.Close();
                _currentFlyout = null;
                return;
            }

            if (!GetWindowRect(WindowNative.GetWindowHandle(this), out RECT rect))
            {
                return;
            }

            double scale = MainBorder.XamlRoot?.RasterizationScale ?? 1.0;
            var anchorRect = new Rect(
                rect.Left / scale,
                rect.Top / scale,
                (rect.Right - rect.Left) / scale,
                (rect.Bottom - rect.Top) / scale);

            _currentFlyout = new WeatherFlyout(_data, _loc, anchorRect, false);
            _currentFlyout.Closed += Flyout_Closed;
            _currentFlyout.Activate();
        }

        private void Flyout_Closed(object sender, object args)
        {
            _currentFlyout = null;
            _loc = null;
            DispatcherQueue.TryEnqueue(async () => await LoadData());
        }

        private void Widget_RightClick(object sender, RightTappedRoutedEventArgs e)
        {
            if (RootGrid.Resources["WidgetMenu"] is MenuFlyout menu)
            {
                menu.ShowAt(MainBorder);
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private void OpenSettings()
        {
            if (_data == null || _loc == null)
            {
                return;
            }

            if (!GetWindowRect(WindowNative.GetWindowHandle(this), out RECT rect))
            {
                return;
            }

            double scale = MainBorder.XamlRoot?.RasterizationScale ?? 1.0;
            var anchorRect = new Rect(
                rect.Left / scale,
                rect.Top / scale,
                (rect.Right - rect.Left) / scale,
                (rect.Bottom - rect.Top) / scale);

            _currentFlyout?.Close();
            _currentFlyout = new WeatherFlyout(_data, _loc, anchorRect, true);
            _currentFlyout.Closed += Flyout_Closed;
            _currentFlyout.Activate();
        }

        private void Widget_MouseEnter(object sender, PointerRoutedEventArgs e)
        {
            _isHovering = true;
            AnimateBackground(true);
        }

        private void Widget_MouseLeave(object sender, PointerRoutedEventArgs e)
        {
            _isHovering = false;
            AnimateBackground(false);
        }

        private void AnimateBackground(bool show)
        {
            var color = show
                ? Windows.UI.Color.FromArgb(40, 255, 255, 255)
                : Windows.UI.Color.FromArgb(0, 255, 255, 255);
            if (MainBorder.Background is not SolidColorBrush brush)
            {
                brush = new SolidColorBrush(color);
                MainBorder.Background = brush;
            }
            brush.Color = color;
        }

        private void SyncWithTaskbarState()
        {
            if (_data == null || _appWindow == null)
            {
                return;
            }

            if (!IsTaskbarCurrentlyVisible())
            {
                _currentFlyout?.Close();
                return;
            }

            EnsureWidgetOnTop();
        }

        private void EnsureWidgetOnTop()
        {
            if (_isHovering || _appWindow == null)
            {
                return;
            }

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            _ = SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private void DetectTaskbarPosition()
        {
            APPBARDATA abd = new APPBARDATA();
            abd.cbSize = Marshal.SizeOf(abd);
            IntPtr result = SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);

            _taskbarEdge = result != IntPtr.Zero ? abd.uEdge : ABE_BOTTOM;
        }

        private void PositionWidget()
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            IntPtr tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);

            if (tray != IntPtr.Zero && GetWindowRect(tray, out RECT trayRect) && GetWindowRect(taskbar, out RECT tbRect))
            {
                double scale = MainBorder.XamlRoot?.RasterizationScale ?? 1.0;

                switch (_taskbarEdge)
                {
                    case ABE_BOTTOM:
                        PositionForBottomTaskbar(trayRect, tbRect, scale);
                        break;
                    case ABE_TOP:
                        PositionForTopTaskbar(trayRect, tbRect, scale);
                        break;
                    case ABE_LEFT:
                        PositionForLeftTaskbar(trayRect, tbRect, scale);
                        break;
                    case ABE_RIGHT:
                        PositionForRightTaskbar(trayRect, tbRect, scale);
                        break;
                }
            }
        }

        private void PositionForBottomTaskbar(RECT trayRect, RECT tbRect, double scale)
        {
            double tbHeight = (tbRect.Bottom - tbRect.Top) / scale;
            double trayLeft = trayRect.Left / scale;

            MoveTo(trayLeft - (MainBorder.ActualWidth + 16), (tbRect.Top / scale) + ((tbHeight - MainBorder.ActualHeight) / 2));
        }

        private void PositionForTopTaskbar(RECT trayRect, RECT tbRect, double scale)
        {
            double tbHeight = (tbRect.Bottom - tbRect.Top) / scale;
            double trayLeft = trayRect.Left / scale;

            MoveTo(trayLeft - (MainBorder.ActualWidth + 16), (tbRect.Top / scale) + ((tbHeight - MainBorder.ActualHeight) / 2));
        }

        private void PositionForLeftTaskbar(RECT trayRect, RECT tbRect, double scale)
        {
            double tbWidth = (tbRect.Right - tbRect.Left) / scale;
            double trayTop = trayRect.Top / scale;

            MoveTo((tbRect.Left / scale) + ((tbWidth - MainBorder.ActualWidth) / 2), trayTop - (MainBorder.ActualHeight + 16));
        }

        private void PositionForRightTaskbar(RECT trayRect, RECT tbRect, double scale)
        {
            double tbWidth = (tbRect.Right - tbRect.Left) / scale;
            double trayTop = trayRect.Top / scale;

            MoveTo((tbRect.Left / scale) + ((tbWidth - MainBorder.ActualWidth) / 2), trayTop - (MainBorder.ActualHeight + 16));
        }

        private void MoveTo(double left, double top)
        {
            if (_appWindow == null)
            {
                return;
            }

            double scale = MainBorder.XamlRoot?.RasterizationScale ?? 1.0;
            var point = new PointInt32((int)Math.Round(left * scale), (int)Math.Round(top * scale));
            _appWindow.Move(point);
        }

        private bool IsTaskbarCurrentlyVisible()
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero)
            {
                return true;
            }

            if (!IsWindowVisible(taskbar))
            {
                return false;
            }

            if (!GetWindowRect(taskbar, out RECT taskbarRect))
            {
                return true;
            }

            int width = taskbarRect.Right - taskbarRect.Left;
            int height = taskbarRect.Bottom - taskbarRect.Top;

            APPBARDATA abd = new APPBARDATA
            {
                cbSize = Marshal.SizeOf(typeof(APPBARDATA))
            };
            int state = (int)SHAppBarMessage(ABM_GETSTATE, ref abd);
            bool autoHide = (state & ABS_AUTOHIDE) != 0;

            if (autoHide)
            {
                if (_taskbarEdge == ABE_TOP || _taskbarEdge == ABE_BOTTOM)
                {
                    return height > 2;
                }

                return width > 2;
            }

            return true;
        }
    }
}
