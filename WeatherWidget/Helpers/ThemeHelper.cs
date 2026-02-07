using System.Windows.Media;
using Microsoft.Win32;

namespace WeatherWidget.Helpers
{
    public static class ThemeHelper
    {
        public static Color GetForegroundColor()
        {
            var res = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
            return (res?.ToString() == "0") ? Colors.White : Colors.Black;
        }
        public static SolidColorBrush GetTaskbarBrush() => new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
    }
}