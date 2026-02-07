using System;
using Windows.Storage;

namespace WeatherWidget.Services
{
    public static class SettingsService
    {
        private static ApplicationDataContainer Local => ApplicationData.Current.LocalSettings;

        public static bool UseManualLocation
        {
            get => GetBool(nameof(UseManualLocation), false);
            set => Local.Values[nameof(UseManualLocation)] = value;
        }

        public static double ManualLatitude
        {
            get => GetDouble(nameof(ManualLatitude), 35.44);
            set => Local.Values[nameof(ManualLatitude)] = value;
        }

        public static double ManualLongitude
        {
            get => GetDouble(nameof(ManualLongitude), -89.81);
            set => Local.Values[nameof(ManualLongitude)] = value;
        }

        public static bool StartWithWindows
        {
            get => GetBool(nameof(StartWithWindows), false);
            set => Local.Values[nameof(StartWithWindows)] = value;
        }

        private static bool GetBool(string key, bool fallback)
        {
            if (Local.Values.TryGetValue(key, out object? value))
            {
                if (value is bool b)
                {
                    return b;
                }

                if (value is string s && bool.TryParse(s, out bool parsed))
                {
                    return parsed;
                }
            }
            return fallback;
        }

        private static double GetDouble(string key, double fallback)
        {
            if (Local.Values.TryGetValue(key, out object? value))
            {
                if (value is double d)
                {
                    return d;
                }

                if (value is float f)
                {
                    return f;
                }

                if (value is string s && double.TryParse(s, out double parsed))
                {
                    return parsed;
                }
            }
            return fallback;
        }
    }
}
