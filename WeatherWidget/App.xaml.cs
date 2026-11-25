using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using WeatherWidget.Services;

namespace WeatherWidget
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private const string AppId = "WeatherWidget-2026-Instance";
        private static readonly string LogDirectory = AppStorage.DataDirectory;
        private static readonly string LogPath = AppStorage.CrashLogPath;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, AppId, out bool isNewInstance);
            if (!isNewInstance)
            {
                IntPtr hWnd = FindWindow(null, "Weather");
                if (hWnd != IntPtr.Zero)
                {
                    _ = PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    Thread.Sleep(200);
                }
                _mutex = new Mutex(true, AppId);
            }

            Directory.CreateDirectory(LogDirectory);
            var settings = AppSettingsStore.Load();
            settings.StartWithWindows = StartupService.IsEnabled();
            AppSettingsStore.Save(settings);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

            // IMPORTANT: Use WindowBackdropType.None here.
            // TaskbarWidget uses AllowsTransparency="True", which creates a WS_EX_LAYERED window
            // and uses UpdateLayeredWindow for per-pixel alpha compositing. If WindowBackdropType
            // is set to anything other than None, WPF-UI registers a global hook and calls
            // DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE, Mica/Acrylic/…) on every window
            // it can reach — including TaskbarWidget. DWM backdrop APIs are incompatible with
            // WS_EX_LAYERED: the DWM call causes the compositor to repaint the layered surface
            // with an opaque white background, permanently destroying the per-pixel transparency.
            // WeatherFlyout (a FluentWindow) manages its own Mica backdrop via its XAML
            // WindowBackdropType="Mica" property and does not rely on this global setting.
            TaskbarThemeService.ApplyTaskbarTheme();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            _mutex?.Dispose();
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            SafeLog("Dispatcher", e.Exception);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            SafeLog("AppDomain", e.ExceptionObject as Exception);
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            SafeLog("TaskScheduler", e.Exception);
            e.SetObserved();
        }

        private void SystemEvents_UserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General
                && e.Category != UserPreferenceCategory.Color
                && e.Category != UserPreferenceCategory.VisualStyle)
            {
                return;
            }

            if (Dispatcher.CheckAccess())
            {
                TaskbarThemeService.ApplyTaskbarTheme();
                return;
            }

            Dispatcher.BeginInvoke(new Action(() => TaskbarThemeService.ApplyTaskbarTheme()));
        }

        private void SafeLog(string source, Exception? ex)
        {
            try
            {
                if (ex == null) return;
                File.AppendAllText(LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}] {ex}\r\n\r\n");
            }
            catch
            {
                // Swallow logging errors to avoid secondary crashes.
            }
        }
    }

    internal static class HttpClients
    {
        public static readonly HttpClient Instance = new() { Timeout = TimeSpan.FromSeconds(10) };
    }
}
