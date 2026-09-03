using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("shell32.dll")]
        private static extern IntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const uint MONITOR_DEFAULTTOPRIMARY = 0;
        private const uint MONITORINFOF_PRIMARY = 0x1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

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
        private const int ABM_GETSTATE = 4;
        private const int ABM_GETTASKBARPOS = 5;
        private const int ABE_LEFT = 0;
        private const int ABE_TOP = 1;
        private const int ABE_RIGHT = 2;
        private const int ABE_BOTTOM = 3;
        private const int ABS_AUTOHIDE = 0x1;

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

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private const int GWL_STYLE = -16;
        private const int WS_CHILD = 0x40000000;
        private const int WS_POPUP = unchecked((int)0x80000000);

        private const uint EVENT_OBJECT_REORDER = 0x8004;
        private const uint EVENT_OBJECT_SHOW = 0x8002;
        private const uint EVENT_OBJECT_HIDE = 0x8003;
        private const uint EVENT_OBJECT_ACTIVATE = 0x0006;
        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint WINEVENT_SKIPOWNPROCESS = 2;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;

        private const uint GA_ROOT = 2;

        private static readonly IntPtr HWND_TOP = new(0);

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEvent, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        // WM_CONTEXTMENU: generated by DefWindowProc when it processes WM_RBUTTONUP.
        // Per the documented Win32 contract, "if a window is a child window, DefWindowProc
        // sends the message to the parent" when the window does not itself handle it
        // (see learn.microsoft.com/windows/win32/menurc/wm-contextmenu). Because this Window
        // is a WS_CHILD of Shell_TrayWnd (a window owned by a different process, explorer.exe)
        // for z-order purposes, that forwarding is cross-process: explorer's tray window
        // procedure receives a WM_CONTEXTMENU for an HWND it does not own or expect, which is
        // what produced the freeze/crash. The fix is to intercept WM_CONTEXTMENU at the
        // native message level (see WndProc), suppress the default cross-process forward, and
        // re-post it to Shell_TrayWnd with the taskbar's own HWND as wParam so explorer
        // treats it as a normal taskbar right-click and shows the taskbar context menu.
        private const int WM_CONTEXTMENU = 0x007B;
        private const int WM_WINDOWPOSCHANGING = 0x0046;

        private const int DWMWA_DISALLOW_PEEK = 11;

        private const uint EVENT_OBJECT_CREATE = 0x8000;
        private const uint EVENT_OBJECT_DESTROY = 0x8001;
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        private const uint EVENT_SYSTEM_DESKTOPSWITCH = 0x0032;

        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const uint SWP_NOZORDER = 0x0004;

        private HwndSource? _hwndSource;

        private readonly WeatherService _ws = new();
        private readonly LocationService _ls = new();
        private WeatherData? _data;
        private LocationData? _loc;

        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _dynamicTimer;
        private int _lastDynamicHour = -1;

        private WeatherFlyout? _currentFlyout;
        private bool _isTaskbarDark = true;
        private int _taskbarEdge = ABE_BOTTOM;
        private int _originalExStyle;
        private bool _isFullscreenAppActive = false;

        private IntPtr _taskbarHwnd = IntPtr.Zero;
        private IntPtr _winEventHook = IntPtr.Zero;
        private uint _taskbarPid = 0;
        private WinEventDelegate? _winEventDelegate;
        private IntPtr _myHwnd = IntPtr.Zero;
        private IntPtr _winEventHookGlobal = IntPtr.Zero;
        private IntPtr _winEventHookDesktopSwitch = IntPtr.Zero;
        private volatile int _reorderPending;
        private readonly StringBuilder _classNameBuffer = new(256);
        private bool? _cachedIsCentered;

        public TaskbarWidget()
        {
            InitializeComponent();

            this.Opacity = 1.0;
            this.Left = -10000;
            this.Top = -10000;

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _refreshTimer.Tick += async (s, e) => await LoadData();
            _refreshTimer.Start();

            _dynamicTimer = new DispatcherTimer();
            _dynamicTimer.Tick += DynamicTimer_Tick;
            ScheduleNextHourBoundary();

            _isTaskbarDark = TaskbarThemeService.IsTaskbarDark();
            ApplyWidgetTheme();
        }

        private void MaintainZOrder()
        {
            try
            {
                if (!IsLoaded) return;
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                // Initial positioning if the widget has never been placed
                if (this.Left < -5000 || this.Top < -5000)
                {
                    DetectTaskbarPosition();
                    PositionWidget();
                }

                // Fullscreen detection — this is the single source of truth
                bool isFullscreenNow = IsFullscreenApplicationActive();
                if (isFullscreenNow != _isFullscreenAppActive)
                    _isFullscreenAppActive = isFullscreenNow;

                if (_isFullscreenAppActive)
                {
                    if (this.Visibility != Visibility.Collapsed)
                        this.Visibility = Visibility.Collapsed;
                    return;
                }

                // Restore visibility when no fullscreen app is active
                if (this.Visibility != Visibility.Visible)
                    this.Visibility = Visibility.Visible;

                // Reassert topmost — only when no flyout is open so the flyout
                // naturally stays on top
                if (_currentFlyout == null)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
            catch { }
        }

        private void SystemEvents_UserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General
                && e.Category != UserPreferenceCategory.Color
                && e.Category != UserPreferenceCategory.VisualStyle)
                return;

            Dispatcher.BeginInvoke(new Action(UpdateTextColor));
        }

        private void UpdateTextColor()
        {
            bool isDark = TaskbarThemeService.IsTaskbarDark();
            if (isDark != _isTaskbarDark)
            {
                _isTaskbarDark = isDark;
                ApplyWidgetTheme();
            }
        }

        private void ApplyWidgetTheme()
        {
            var textBrush = new SolidColorBrush(_isTaskbarDark ? Colors.White : Color.FromRgb(32, 32, 32));
            textBrush.Freeze();

            TempText.Foreground = textBrush;
            ConditionText.Foreground = textBrush;

            // Hover effect: lightens the background — like Windows 11 taskbar icon hover.
            // No border — just a semi-transparent white pill that fades in.
            var fillBrush = new SolidColorBrush(Color.FromArgb(0x80, 255, 255, 255));
            fillBrush.Freeze();
            HoverOverlay.Background = fillBrush;
            HoverOverlay.BorderBrush = new SolidColorBrush(Color.FromArgb(0x1A, 0, 0, 0));
            HoverOverlay.BorderThickness = new Thickness(0.5);

            TaskbarThemeService.ApplyTaskbarTheme();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // Hook the native window procedure so we can swallow WM_CONTEXTMENU before
            // DefWindowProc ever sees it. This prevents the right-click freeze/crash.
            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);

            // Make the window a non-activatable tool window.
            // Preserve all existing style bits (including WS_EX_LAYERED that WPF set for
            // AllowsTransparency) — only OR in the new bits.
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE).ToInt32();
            _originalExStyle = exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetWindowLong(hwnd, GWL_EXSTYLE, new IntPtr(_originalExStyle));

            // NOTE: Do NOT call DwmSetWindowAttribute(DWMWA_NCRENDERING_POLICY, DWMNCRP_DISABLED)
            // here. On Windows 11 that attribute interferes with the WS_EX_LAYERED compositing
            // pipeline used by AllowsTransparency="True" and causes a permanent white background.
            // A WindowStyle="None" + AllowsTransparency window has no NC chrome or shadow anyway.

            // Exclude this window from "Peek at desktop" and similar desktop-preview operations.
            // DWMWA_DISALLOW_PEEK prevents DWM from hiding the widget during peek/show-desktop.
            int peekVal = 1;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_DISALLOW_PEEK, ref peekVal, sizeof(int));

            // Force WPF to re-render the transparent background after the style change.
            // SetWindowLong can cause Win32 to issue a WM_ERASEBKGND before WPF redraws;
            // InvalidateVisual queues a fresh WPF render pass to restore the transparent surface.
            this.InvalidateVisual();

            // NOTE: Do NOT call ApplyRoundedClip (i.e., set this.Clip) here.

            // DO NOT use SetParent() or make this window a WS_CHILD of Shell_TrayWnd.
            // That creates a shared input queue between this thread and explorer's thread,
            // causing synchronous cross-thread calls that can deadlock when mouse capture or
            // focus changes occur. Instead, we manage z-order independently using HWND_TOPMOST
            // and reactive SetWindowPos calls based on WinEventHook notifications.
            _myHwnd = hwnd;
            _taskbarHwnd = FindPrimaryTaskbar();
            _winEventDelegate = TaskbarWinEventProc;    // Set before registering any hook

            if (_taskbarHwnd != IntPtr.Zero)
            {
                GetWindowThreadProcessId(_taskbarHwnd, out _taskbarPid);

                // Subscribe to the full lifecycle + location event range for the taskbar
                // process: EVENT_OBJECT_CREATE (0x8000) through EVENT_OBJECT_LOCATIONCHANGE
                // (0x800B). This covers create, destroy, show, hide, activate, z-order
                // reorder, and position/size changes of all taskbar child windows (including
                // flyouts).
                _winEventHook = SetWinEventHook(
                    EVENT_OBJECT_CREATE, EVENT_OBJECT_LOCATIONCHANGE,
                    IntPtr.Zero, _winEventDelegate,
                    _taskbarPid, 0,
                    WINEVENT_OUTOFCONTEXT);
            }

            // Global foreground hook: fires whenever ANY window becomes foreground.
            // This is the critical path for catching the Windows 11 taskbar XAML island
            // asserting itself to the top of the TOPMOST z-stack (e.g. Start menu open,
            // flyout open, clicking the taskbar body).  WINEVENT_SKIPOWNPROCESS ensures
            // we never react to our own window changes and create a feedback loop.
            _winEventHookGlobal = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _winEventDelegate,
                0, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

            // Global desktop-switch hook: fires when the user switches virtual desktops
            // (Win+Ctrl+Left/Right). The same coalesced re-order logic reasserts TOPMOST
            // after the transition completes.
            _winEventHookDesktopSwitch = SetWinEventHook(
                EVENT_SYSTEM_DESKTOPSWITCH, EVENT_SYSTEM_DESKTOPSWITCH,
                IntPtr.Zero, _winEventDelegate,
                0, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

            DetectTaskbarPosition();
            PositionWidget();
            PinWidgetToTaskbarZOrder(forceShow: true);

            await LoadData();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // No ApplyRoundedClip call here — see Window_Loaded note above.
        }

        private void DetectTaskbarPosition()
        {
            APPBARDATA abd = new APPBARDATA();
            abd.cbSize = Marshal.SizeOf(abd);
            IntPtr result = SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);
            _taskbarEdge = result != IntPtr.Zero ? abd.uEdge : ABE_BOTTOM;
            _cachedIsCentered = null;
        }

        /// <summary>
        /// Resolves the primary-monitor taskbar ("Shell_TrayWnd"). Multi-monitor tray mods
        /// (e.g. WindHawk's taskbar-on-all-monitors style mods) can create or reorder
        /// additional windows so that a plain FindWindow("Shell_TrayWnd", null) — which only
        /// ever returns the first Z-order match — intermittently resolves to a secondary
        /// monitor's taskbar instead of the primary one. That, in turn, is why the widget was
        /// observed jumping to the secondary monitor, especially right after the flyout
        /// triggers a Z-order/foreground shuffle. This walks every top-level window with that
        /// class name and returns the one that actually sits on the primary monitor.
        /// </summary>
        private static IntPtr FindPrimaryTaskbar()
        {
            IntPtr primary = IntPtr.Zero;
            IntPtr fallback = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                var buffer = new StringBuilder(256);
                GetClassName(hWnd, buffer, buffer.Capacity);
                if (!string.Equals(buffer.ToString(), "Shell_TrayWnd", StringComparison.Ordinal))
                    return true;

                if (fallback == IntPtr.Zero)
                    fallback = hWnd;

                IntPtr monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTOPRIMARY);
                if (monitor != IntPtr.Zero)
                {
                    var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                    if (GetMonitorInfo(monitor, ref mi) && (mi.dwFlags & MONITORINFOF_PRIMARY) != 0)
                    {
                        primary = hWnd;
                        return false; // found it, stop enumerating
                    }
                }

                return true;
            }, IntPtr.Zero);

            // Fall back to whatever Shell_TrayWnd we found (or Zero) if none reported as
            // being on the primary monitor — better to degrade to prior behavior than fail.
            return primary != IntPtr.Zero ? primary : fallback;
        }

        private static bool IsWindowOnPrimaryMonitor(IntPtr hWnd)
        {
            IntPtr monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTOPRIMARY);
            if (monitor == IntPtr.Zero) return false;

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            return GetMonitorInfo(monitor, ref mi) && (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
        }

        private async Task LoadData()
        {
            try
            {
                _loc ??= await _ls.GetCurrentLocationAsync();
                if (_loc != null)
                    _data = await _ws.GetWeatherDataAsync(_loc.Latitude, _loc.Longitude);

                if (_data == null)
                {
                    TempText.Text = "--°";
                    ConditionText.Text = "Weather unavailable";
                    WeatherIcon.Source = null;
                }
                else
                {
                    double temp = _data.Temperature;
                    // Use time of day to determine heating/cooling phase.
                    // Daily high typically occurs around 2-4 PM; before 3 PM = heating
                    // toward the high, after 3 PM = cooling toward the low.
                    double suffix = DateTime.Now.Hour < 15
                        ? _data.HighTemp
                        : _data.LowTemp;
                    TempText.Text = $"{temp:N0}° / {suffix:N0}°";
                    ConditionText.Text = _data.Condition;
                    _lastDynamicHour = DateTime.Now.Hour;
                    try
                    {
                        WeatherIcon.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/PNG/{_data.IconCode}.png"));
                    }
                    catch
                    {
                        WeatherIcon.Source = null;
                    }
                }

                if (this.IsLoaded)
                {
                    this.UpdateLayout();
                    PositionWidget();

                    this.Visibility = Visibility.Visible;
                    PinWidgetToTaskbarZOrder(forceShow: false);
                }
            }
            catch { }
        }

        private void ScheduleNextHourBoundary()
        {
            var now = DateTime.Now;
            var nextHour = now.Date.AddHours(now.Hour + 1);
            _dynamicTimer.Interval = nextHour - now;
            _dynamicTimer.Start();
        }

        private void DynamicTimer_Tick(object? sender, EventArgs e)
        {
            _dynamicTimer.Stop();

            if (_data == null || _data.HourlyForecast.Count == 0)
            {
                ScheduleNextHourBoundary();
                return;
            }

            var now = DateTime.Now;
            var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            var match = _data.HourlyForecast.FirstOrDefault(h => h.Date == currentHour);
            if (match == null)
            {
                _lastDynamicHour = now.Hour;
                ScheduleNextHourBoundary();
                return;
            }

            _lastDynamicHour = now.Hour;

            try
            {
                ConditionText.Text = match.Condition;
                WeatherIcon.Source = new BitmapImage(new Uri($"pack://application:,,,{match.IconPath}"));
            }
            catch
            {
                WeatherIcon.Source = null;
            }

            ScheduleNextHourBoundary();
        }

        private static IntPtr? FindWidgetsButtonHwnd(IntPtr taskbar, StringBuilder nameBuffer)
        {
            IntPtr found = IntPtr.Zero;
            EnumChildWindows(taskbar, (hWnd, lParam) =>
            {
                nameBuffer.Clear();
                GetClassName(hWnd, nameBuffer, nameBuffer.Capacity);
                string cls = nameBuffer.ToString();

                nameBuffer.Clear();
                GetWindowText(hWnd, nameBuffer, nameBuffer.Capacity);
                string text = nameBuffer.ToString();

                if (cls.Contains("Widget", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Widget", StringComparison.OrdinalIgnoreCase))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found != IntPtr.Zero ? found : null;
        }

        private static IntPtr? FindStartButtonHwnd(IntPtr taskbar, StringBuilder nameBuffer)
        {
            IntPtr found = IntPtr.Zero;

            // Strategy 1: "Button" class with "Start" text (classic Windows)
            EnumChildWindows(taskbar, (hWnd, lParam) =>
            {
                nameBuffer.Clear();
                GetClassName(hWnd, nameBuffer, nameBuffer.Capacity);
                string cls = nameBuffer.ToString();
                nameBuffer.Clear();
                GetWindowText(hWnd, nameBuffer, nameBuffer.Capacity);
                string text = nameBuffer.ToString();

                if (cls.Contains("Button", StringComparison.OrdinalIgnoreCase) &&
                    text.Contains("Start", StringComparison.OrdinalIgnoreCase))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            if (found != IntPtr.Zero) return found;

            // Strategy 2: Any window with "Start" in its text, near the left edge
            EnumChildWindows(taskbar, (hWnd, lParam) =>
            {
                nameBuffer.Clear();
                GetWindowText(hWnd, nameBuffer, nameBuffer.Capacity);
                string text = nameBuffer.ToString();

                if (text.Contains("Start", StringComparison.OrdinalIgnoreCase) &&
                    GetWindowRect(hWnd, out RECT sr) && GetWindowRect(taskbar, out RECT st))
                {
                    if (sr.Left - st.Left < 200)
                    {
                        found = hWnd;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);
            if (found != IntPtr.Zero) return found;

            // Strategy 3: Any child with "Start" in its class name
            EnumChildWindows(taskbar, (hWnd, lParam) =>
            {
                nameBuffer.Clear();
                GetClassName(hWnd, nameBuffer, nameBuffer.Capacity);
                string cls = nameBuffer.ToString();

                if (cls.Contains("Start", StringComparison.OrdinalIgnoreCase))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return found != IntPtr.Zero ? found : null;
        }

        private double CalculateCornerGroupRightEdge(IntPtr taskbar, double dpiScaleX)
        {
            var startHwnd = FindStartButtonHwnd(taskbar, _classNameBuffer);
            if (!startHwnd.HasValue) return 0;
            if (!GetWindowRect(startHwnd.Value, out RECT startRect)) return 0;

            int maxRight = startRect.Right;

            EnumChildWindows(taskbar, (hWnd, lParam) =>
            {
                if (hWnd == startHwnd.Value) return true;
                if (!GetWindowRect(hWnd, out RECT r)) return true;

                // Must be within 400 physical pixels of start button's left
                if (r.Left > startRect.Left + 400) return true;

                // Must vertically overlap with the start button area
                if (r.Bottom < startRect.Top || r.Top > startRect.Bottom) return true;

                // Skip overlarge windows (the whole taskbar, desktop, etc.)
                int w = r.Right - r.Left;
                int h = r.Bottom - r.Top;
                if (w < 10 || w > 250) return true;
                if (h < 20 || h > (startRect.Bottom - startRect.Top) * 2) return true;

                if (r.Right > maxRight)
                    maxRight = r.Right;

                return true;
            }, IntPtr.Zero);

            return (maxRight / dpiScaleX) + 4;
        }

        private bool TryPositionAtWidgetsButton(IntPtr taskbar, DpiScale dpi, out double x, out double y)
        {
            x = y = 0;
            var widgetsHwnd = FindWidgetsButtonHwnd(taskbar, _classNameBuffer);
            if (!widgetsHwnd.HasValue || !GetWindowRect(widgetsHwnd.Value, out RECT wRect)) return false;
            if (!GetWindowRect(taskbar, out RECT tbRect)) return false;

            switch (_taskbarEdge)
            {
                case ABE_BOTTOM:
                case ABE_TOP:
                    x = (wRect.Left / dpi.DpiScaleX) - 4;
                    y = (tbRect.Top / dpi.DpiScaleY) + (((tbRect.Bottom - tbRect.Top) / dpi.DpiScaleY) - this.ActualHeight) / 2;
                    break;
                default:
                    x = (tbRect.Left / dpi.DpiScaleX) + (((tbRect.Right - tbRect.Left) / dpi.DpiScaleX) - this.ActualWidth) / 2;
                    y = (wRect.Top / dpi.DpiScaleY) - this.ActualHeight - 4;
                    break;
            }
            return true;
        }

        private bool TryPositionAtCornerGroup(IntPtr taskbar, DpiScale dpi, out double x, out double y)
        {
            x = y = 0;
            if (!GetWindowRect(taskbar, out RECT tbRect)) return false;

            double cornerRight = CalculateCornerGroupRightEdge(taskbar, dpi.DpiScaleX);
            if (cornerRight <= 0) return false;

            switch (_taskbarEdge)
            {
                case ABE_BOTTOM:
                case ABE_TOP:
                    x = cornerRight;
                    y = (tbRect.Top / dpi.DpiScaleY) + (((tbRect.Bottom - tbRect.Top) / dpi.DpiScaleY) - this.ActualHeight) / 2;
                    break;
                default:
                    x = (tbRect.Left / dpi.DpiScaleX) + (((tbRect.Right - tbRect.Left) / dpi.DpiScaleX) - this.ActualWidth) / 2;
                    y = cornerRight - this.ActualHeight - 4;
                    break;
            }
            return true;
        }

        private bool TryPositionAtTray(IntPtr taskbar, DpiScale dpi, out double x, out double y)
        {
            x = y = 0;
            IntPtr tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            if (tray == IntPtr.Zero || !GetWindowRect(tray, out RECT trayRect)) return false;
            if (!GetWindowRect(taskbar, out RECT tbRect)) return false;

            switch (_taskbarEdge)
            {
                case ABE_BOTTOM:
                case ABE_TOP:
                    x = (trayRect.Left / dpi.DpiScaleX) - this.ActualWidth - 8;
                    y = (tbRect.Top / dpi.DpiScaleY) + (((tbRect.Bottom - tbRect.Top) / dpi.DpiScaleY) - this.ActualHeight) / 2;
                    break;
                default:
                    x = (tbRect.Left / dpi.DpiScaleX) + (((tbRect.Right - tbRect.Left) / dpi.DpiScaleX) - this.ActualWidth) / 2;
                    y = (trayRect.Top / dpi.DpiScaleY) - this.ActualHeight - 8;
                    break;
            }
            return true;
        }

        private bool IsTaskbarCentered()
        {
            if (_cachedIsCentered.HasValue)
                return _cachedIsCentered.Value;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                if (key?.GetValue("TaskbarAl") is int taskbarAl)
                    _cachedIsCentered = taskbarAl == 1;
                else
                    _cachedIsCentered = false;
            }
            catch
            {
                _cachedIsCentered = false;
            }

            return _cachedIsCentered.Value;
        }

        private void PositionWidget()
        {
            IntPtr taskbar = _taskbarHwnd;

            // Re-validate that our cached taskbar handle is still the primary-monitor one.
            // Multi-monitor tray mods can reorder/recreate Shell_TrayWnd windows, especially
            // right around flyout open/close; if the cached handle no longer resolves to the
            // primary monitor (or is stale), re-resolve it rather than silently positioning
            // the widget on whatever monitor that handle now belongs to.
            if (taskbar == IntPtr.Zero || !IsWindowOnPrimaryMonitor(taskbar))
            {
                taskbar = FindPrimaryTaskbar();
                if (taskbar != IntPtr.Zero)
                    _taskbarHwnd = taskbar;
            }
            if (taskbar == IntPtr.Zero) return;

            var dpi = VisualTreeHelper.GetDpi(this);

            // Tier 1: native Widgets button works for any alignment
            if (TryPositionAtWidgetsButton(taskbar, dpi, out var sx, out var sy))
            {
                this.Left = sx; this.Top = sy;
                return;
            }

            // Tier 2: alignment-aware positioning
            bool centered = IsTaskbarCentered();
            bool positioned = centered
                ? TryPositionAtCornerGroup(taskbar, dpi, out sx, out sy)
                : TryPositionAtTray(taskbar, dpi, out sx, out sy);

            // Tier 3: fall back to the other strategy
            if (!positioned)
            {
                positioned = centered
                    ? TryPositionAtTray(taskbar, dpi, out sx, out sy)
                    : TryPositionAtCornerGroup(taskbar, dpi, out sx, out sy);
            }

            if (positioned)
            {
                this.Left = sx;
                this.Top = sy;
            }
        }

        private void Widget_MouseEnter(object sender, MouseEventArgs e)
        {
            AnimateBackground(true);
        }

        private void Widget_MouseLeave(object sender, MouseEventArgs e)
        {
            AnimateBackground(false);
        }

        private void AnimateBackground(bool show)
        {
            // Win11 taskbar icon hover: symmetric ~150ms with easing in both directions.
            var duration = TimeSpan.FromMilliseconds(150);
            var anim = new DoubleAnimation(show ? 1.0 : 0.0, duration)
            {
                EasingFunction = new QuadraticEase
                {
                    EasingMode = show ? EasingMode.EaseOut : EasingMode.EaseIn
                }
            };
            HoverOverlay.BeginAnimation(UIElement.OpacityProperty, anim);
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
            _currentFlyout.Show();
            _currentFlyout.Activate();
        }

        private void Flyout_Closed(object? sender, EventArgs e)
        {
            var flyout = _currentFlyout;
            _currentFlyout = null;

            this.Dispatcher.Invoke(() =>
            {
                if (flyout is WeatherFlyout { SettingsWereSaved: true })
                    _loc = null;

                Dispatcher.BeginInvoke(new Action(async () => await LoadData()));
                PinWidgetToTaskbarZOrder(forceShow: true);
            }, DispatcherPriority.Send);
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

                _currentFlyout = new WeatherFlyout(_data, _loc, anchorRect, startInSettings: true);
                _currentFlyout.Closed += Flyout_Closed;
                _currentFlyout.Show();
                _currentFlyout.Activate();
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

        private bool IsTaskbarCurrentlyVisible()
        {
            IntPtr taskbar = FindPrimaryTaskbar();
            if (taskbar == IntPtr.Zero) return true;
            if (!IsWindowVisible(taskbar)) return false;
            if (!GetWindowRect(taskbar, out RECT taskbarRect)) return true;

            int width = taskbarRect.Right - taskbarRect.Left;
            int height = taskbarRect.Bottom - taskbarRect.Top;

            APPBARDATA abd = new APPBARDATA { cbSize = Marshal.SizeOf(typeof(APPBARDATA)) };
            int state = (int)SHAppBarMessage(ABM_GETSTATE, ref abd);
            bool autoHide = (state & ABS_AUTOHIDE) != 0;

            if (autoHide)
            {
                return (_taskbarEdge == ABE_TOP || _taskbarEdge == ABE_BOTTOM)
                    ? height > 2
                    : width > 2;
            }

            return true;
        }

        private bool IsFullscreenApplicationActive()
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;

                var hwnd = new WindowInteropHelper(this).Handle;
                if (fg == hwnd) return false;

                // Exclude tool windows — they are never true fullscreen apps.
                int fgExStyle = GetWindowLong(fg, GWL_EXSTYLE).ToInt32();
                if ((fgExStyle & WS_EX_TOOLWINDOW) != 0) return false;

                // Exclude desktop and taskbar windows.
                _classNameBuffer.Clear();
                _ = GetClassName(fg, _classNameBuffer, _classNameBuffer.Capacity);
                string cls = _classNameBuffer.ToString();
                if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;

                // Exclude Windows 11 shell XAML island flyouts (Quick Settings / network /
                // volume / calendar / notification center, etc.). These are hosted in
                // explorer.exe as a "XamlExplorerHostIslandWindow" that is sized to cover the
                // entire monitor (so its own light-dismiss area can catch outside clicks) even
                // though only a small flyout is visually painted. Without this exclusion,
                // opening any of these flyouts satisfies the 95% monitor-coverage heuristic
                // below and the widget is incorrectly hidden as if a fullscreen app launched.
                if (cls == "XamlExplorerHostIslandWindow") return false;

                if (!GetWindowRect(fg, out RECT windowRect)) return false;

                IntPtr monitor = MonitorFromWindow(fg, MONITOR_DEFAULTTOPRIMARY);
                if (monitor == IntPtr.Zero) return false;

                MONITORINFO monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (!GetMonitorInfo(monitor, ref monitorInfo)) return false;

                int monW = monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left;
                int monH = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;
                int winW = windowRect.Right - windowRect.Left;
                int winH = windowRect.Bottom - windowRect.Top;

                // Use a 95% coverage threshold: maximised windows with invisible borders
                // may not perfectly cover the monitor, but 95% is unambiguous.
                return winW >= monW * 0.95 && winH >= monH * 0.95;
            }
            catch
            {
                return false;
            }
        }

        private void PinWidgetToTaskbarZOrder(bool forceShow)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (forceShow)
                ShowWindow(hwnd, SW_SHOWNOACTIVATE);

            MaintainZOrder();
        }

        private void TaskbarWinEventProc(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                // Global events (FOREGROUND, DESKTOPSWITCH) fire from any process.
                // All other events are already pre-filtered to _taskbarPid by the hook.
                bool skipPidCheck = (eventType == EVENT_SYSTEM_FOREGROUND ||
                                     eventType == EVENT_SYSTEM_DESKTOPSWITCH);

                if (!skipPidCheck && hwnd != IntPtr.Zero && hwnd != _taskbarHwnd)
                {
                    uint pid = 0;
                    GetWindowThreadProcessId(hwnd, out pid);
                    if (pid != _taskbarPid)
                        return;
                }

                // ── Corner layout change ──────────────────────────────────────────────────
                // When taskbar children move/resize/appear/disappear (e.g., user toggles
                // Search/Task View visibility, DPI change, corner button reorder),
                // recalculate widget position without a full edge re-detect.
                if ((eventType == EVENT_OBJECT_LOCATIONCHANGE ||
                     eventType == EVENT_OBJECT_CREATE ||
                     eventType == EVENT_OBJECT_DESTROY) &&
                    hwnd != IntPtr.Zero && hwnd != _taskbarHwnd)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (!IsLoaded || _currentFlyout != null) return;
                            PositionWidget();
                        }
                        catch { }
                    }), DispatcherPriority.Normal);
                }

                // ── Taskbar position / visibility change ────────────────────────────────
                // When the taskbar itself moves, resizes, or toggles auto-hide (e.g. unlock,
                // drag, DPI change, reveal from edge), re-detect, reposition, and re-run
                // z-order maintenance so fullscreen detection and topmost are also refreshed.
                if (eventType == EVENT_OBJECT_LOCATIONCHANGE && hwnd == _taskbarHwnd)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (!IsLoaded) return;
                            DetectTaskbarPosition();
                            PositionWidget();
                            MaintainZOrder();
                        }
                        catch { }
                    }), DispatcherPriority.Normal);
                }

                // ── Coalesced z-order maintenance ─────────────────────────────────────────
                // Rapid-fire WinEvents (ACTIVATE, SHOW, REORDER, CREATE, DESTROY, FOREGROUND)
                // all arrive during the same shell transition.  Coalesce them into a single
                // MaintainZOrder call to avoid redundant SetWindowPos repaints.
                // MaintainZOrder handles fullscreen detection, visibility, HWND_TOPMOST, and
                // first-time positioning — everything z-order related in one call.
                if (_myHwnd != IntPtr.Zero && _currentFlyout == null)
                {
                    if (Interlocked.CompareExchange(ref _reorderPending, 1, 0) == 0)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                _reorderPending = 0;
                                if (!IsLoaded) return;
                                MaintainZOrder();
                            }
                            catch { }
                        }), DispatcherPriority.Send);
                    }
                }
            }
            catch { }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CONTEXTMENU)
            {
                // Intercept WM_CONTEXTMENU before DefWindowProc can forward it cross-process
                // (which would send our HWND as wParam to Shell_TrayWnd, causing a freeze).
                // Instead, re-post the message to the taskbar with its own HWND as wParam so
                // explorer treats it as a normal taskbar right-click and shows the taskbar menu.
                handled = true;
                if (_taskbarHwnd != IntPtr.Zero)
                    PostMessage(_taskbarHwnd, WM_CONTEXTMENU, _taskbarHwnd, lParam);
                return IntPtr.Zero;
            }

            if (msg == WM_MOUSEACTIVATE)
            {
                // Return MA_NOACTIVATE for every mouse interaction so this window never
                // steals focus or input-queue ownership from the taskbar or any other
                // window.  WS_EX_NOACTIVATE already blocks most activation paths; this
                // handles the remaining cases (Alt+click, non-client hit-testing, etc.).
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }

            if (msg == WM_WINDOWPOSCHANGING)
            {
                // Intercept z-order demotion attempts. When the system tries to move the
                // widget below the taskbar (e.g. during flyout/Start menu elevation),
                // silently set SWP_NOZORDER to reject the demotion at the kernel level,
                // before DWM renders a single frame with the wrong z-order.
                var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                if (wp.hwndInsertAfter == HWND_BOTTOM || wp.hwndInsertAfter == HWND_NOTOPMOST)
                {
                    wp.flags |= SWP_NOZORDER;
                    Marshal.StructureToPtr(wp, lParam, false);
                    handled = true;
                }
                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;

            if (_winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }

            if (_winEventHookGlobal != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHookGlobal);
                _winEventHookGlobal = IntPtr.Zero;
            }

            if (_winEventHookDesktopSwitch != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHookDesktopSwitch);
                _winEventHookDesktopSwitch = IntPtr.Zero;
            }

            _winEventDelegate = null;
            base.OnClosed(e);
        }
    }
}
