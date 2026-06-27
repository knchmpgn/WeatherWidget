using System;
using System.IO;

namespace WeatherWidget.Services
{
    public static class AppStorage
    {
        public static string DataDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WeatherWidget");

        public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

        public static string CrashLogPath => Path.Combine(DataDirectory, "crash.log");

        public static string LegacySettingsPath => Path.Combine(AppContext.BaseDirectory, "settings.json");
    }
}
