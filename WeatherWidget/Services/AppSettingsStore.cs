using System;
using System.IO;
using System.Text.Json;

namespace WeatherWidget.Services
{
    public sealed class AppSettings
    {
        public bool UseManualLocation { get; set; }

        public double ManualLatitude { get; set; }

        public double ManualLongitude { get; set; }

        public string ManualCityName { get; set; } = "";

        public bool StartWithWindows { get; set; }
    }

    public static class AppSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static AppSettings Load()
        {
            try
            {
                var settingsPath = ResolveSettingsPath();

                var json = File.ReadAllText(settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return LoadLegacySettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            Directory.CreateDirectory(AppStorage.DataDirectory);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(AppStorage.SettingsPath, json);
        }

        private static string ResolveSettingsPath()
        {
            if (File.Exists(AppStorage.SettingsPath))
            {
                return AppStorage.SettingsPath;
            }

            if (File.Exists(AppStorage.LegacySettingsPath))
            {
                return AppStorage.LegacySettingsPath;
            }

            throw new FileNotFoundException("Settings file was not found.");
        }

        private static AppSettings LoadLegacySettings()
        {
            try
            {
                return new AppSettings
                {
                    UseManualLocation = Properties.Settings.Default.UseManualLocation,
                    ManualLatitude = Properties.Settings.Default.ManualLatitude,
                    ManualLongitude = Properties.Settings.Default.ManualLongitude,
                    StartWithWindows = Properties.Settings.Default.StartWithWindows
                };
            }
            catch
            {
                return new AppSettings();
            }
        }
    }
}
