using Microsoft.Win32;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WeatherWidget.Services
{
    public static class TaskbarThemeService
    {
        private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string SystemUsesLightThemeValue = "SystemUsesLightTheme";
        private const string AppsUseLightThemeValue = "AppsUseLightTheme";

        public static bool IsTaskbarDark() => !IsTaskbarLight();

        public static bool IsTaskbarLight()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
                return TryReadThemeValue(key, SystemUsesLightThemeValue)
                    ?? TryReadThemeValue(key, AppsUseLightThemeValue)
                    ?? true;
            }
            catch
            {
                return true;
            }
        }

        public static ApplicationTheme ApplyTaskbarTheme()
        {
            var theme = IsTaskbarLight() ? ApplicationTheme.Light : ApplicationTheme.Dark;

            try
            {
                if (Application.Current != null && ApplicationThemeManager.GetAppTheme() != theme)
                {
                    ApplicationThemeManager.Apply(theme, WindowBackdropType.None, true);
                }
            }
            catch
            {
                // Keep theme synchronization best-effort so rendering stays resilient.
            }

            return theme;
        }

        private static bool? TryReadThemeValue(RegistryKey? key, string valueName)
        {
            object? value = key?.GetValue(valueName);

            return value switch
            {
                int intValue => intValue != 0,
                string stringValue when int.TryParse(stringValue, out int parsedValue) => parsedValue != 0,
                _ => null
            };
        }
    }
}
