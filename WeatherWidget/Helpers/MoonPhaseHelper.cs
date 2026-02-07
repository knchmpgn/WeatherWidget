using System;

namespace WeatherWidget.Services
{
    public static class MoonPhaseHelper
    {
        public static string GetMoonPhaseEmoji(DateTime date)
        {
            double synodicMonth = 29.53058867;
            DateTime knownNewMoon = new DateTime(2000, 1, 6, 12, 24, 1);
            double totalDays = (date - knownNewMoon).TotalDays;
            double normalizedPhase = (totalDays % synodicMonth);
            if (normalizedPhase < 0) normalizedPhase += synodicMonth;

            // Divide the month into 8 phases
            int phaseIndex = (int)(normalizedPhase / (synodicMonth / 8));

            return phaseIndex switch
            {
                0 => "🌑", // New Moon
                1 => "🌒", // Waxing Crescent
                2 => "🌓", // First Quarter
                3 => "🌔", // Waxing Gibbous
                4 => "🌕", // Full Moon
                5 => "🌖", // Waning Gibbous
                6 => "🌗", // Last Quarter
                7 => "🌘", // Waning Crescent
                _ => "🌑"
            };
        }
    }
}