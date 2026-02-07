using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using WeatherWidget.Views;

namespace WeatherWidget
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private const string AppId = "WeatherWidget-2026-Instance";
        private TaskbarWidget? _widget;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        protected override void OnLaunched(LaunchActivatedEventArgs args)
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

            _widget = new TaskbarWidget();
            _widget.Activate();
        }
    }
}
