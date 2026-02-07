using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WeatherWidget.Models;

namespace WeatherWidget.Services
{
    public class WeatherService
    {
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

        public async Task<WeatherData?> GetWeatherDataAsync(double lat, double lon)
        {
            try
            {
                string url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code,is_day,wind_speed_10m,relative_humidity_2m,apparent_temperature,pressure_msl,cloud_cover,visibility&hourly=temperature_2m,weather_code,wind_speed_10m,is_day&daily=sunrise,sunset,temperature_2m_max,temperature_2m_min,weather_code,wind_speed_10m_max,precipitation_probability_max,uv_index_max&temperature_unit=fahrenheit&timezone=auto&wind_speed_unit=mph";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);

                bool isDay = (int)json["current"]!["is_day"]! == 1;
                int currentWeatherCode = (int)json["current"]!["weather_code"]!;
                double currentWindSpeed = (double)json["current"]!["wind_speed_10m"]!;

                var data = new WeatherData
                {
                    Temperature = (double)json["current"]!["temperature_2m"]!,
                    HighTemp = (double)json["daily"]!["temperature_2m_max"]![0]!,
                    LowTemp = (double)json["daily"]!["temperature_2m_min"]![0]!,
                    Condition = MapCodeToString(currentWeatherCode),
                    IconCode = MapCodeToPath(currentWeatherCode, isDay, currentWindSpeed),
                    CurrentHumidity = (int)json["current"]!["relative_humidity_2m"]!,
                    CurrentWindSpeed = currentWindSpeed,
                    FeelsLike = (double)json["current"]!["apparent_temperature"]!,
                    PrecipitationChance = json["daily"]!["precipitation_probability_max"]![0] != null
                        ? (int)json["daily"]!["precipitation_probability_max"]![0]!
                        : 0,
                    Sunrise = (string)json["daily"]!["sunrise"]![0]!,
                    Sunset = (string)json["daily"]!["sunset"]![0]!,
                    Pressure = json["current"]!["pressure_msl"] != null ? (double)json["current"]!["pressure_msl"]! : 0,
                    CloudCover = json["current"]!["cloud_cover"] != null ? (int)json["current"]!["cloud_cover"]! : 0,
                    Visibility = json["current"]!["visibility"] != null ? (double)json["current"]!["visibility"]! : 0,
                    UVIndex = json["daily"]!["uv_index_max"]![0] != null ? (double)json["daily"]!["uv_index_max"]![0]! : 0
                };

                // Hourly Forecast (next 5 items) - use is_day from API
                var hourlyTimes = json["hourly"]?["time"] as JArray;
                int startIndex = 0;
                if (hourlyTimes != null && hourlyTimes.Count > 0)
                {
                    var now = DateTime.Now;
                    startIndex = Math.Max(0, hourlyTimes.Count - 5);
                    for (int i = 0; i < hourlyTimes.Count; i++)
                    {
                        if (DateTime.TryParse((string)hourlyTimes[i]!, out var t) && t >= now)
                        {
                            startIndex = i;
                            break;
                        }
                    }
                }

                int hourlyCount = hourlyTimes?.Count ?? 0;
                int endIndex = Math.Min(startIndex + 5, hourlyCount);
                for (int i = startIndex; i < endIndex; i++)
                {
                    var time = DateTime.Parse((string)json["hourly"]!["time"]![i]!);
                    int hourCode = (int)json["hourly"]!["weather_code"]![i]!;
                    double hourWindSpeed = (double)json["hourly"]!["wind_speed_10m"]![i]!;
                    bool hourIsDay = json["hourly"]!["is_day"]![i] != null && (int)json["hourly"]!["is_day"]![i]! == 1;

                    data.Hourly.Add(new ForecastItem
                    {
                        TimeLabel = time.ToString("t", CultureInfo.CurrentCulture),
                        TempLabel = Math.Round((double)json["hourly"]!["temperature_2m"]![i]!) + "°",
                        IconPath = $"/Assets/PNG/{MapCodeToPath(hourCode, hourIsDay, hourWindSpeed)}.png"
                    });
                }

                // Daily Forecast (5 items) - EXCLUDING TODAY (indices 1-5)
                // For daily forecasts, always use DAY icons since they represent the full day
                for (int i = 1; i <= 5; i++)
                {
                    var time = DateTime.Parse((string)json["daily"]!["time"]![i]!);
                    int dailyCode = (int)json["daily"]!["weather_code"]![i]!;
                    double dailyWindSpeed = (double)json["daily"]!["wind_speed_10m_max"]![i]!;
                    int precipChance = json["daily"]!["precipitation_probability_max"]![i] != null
                        ? (int)json["daily"]!["precipitation_probability_max"]![i]!
                        : 0;

                    // Always use day=true for daily forecasts
                    data.Daily.Add(new ForecastItem
                    {
                        TimeLabel = time.ToString("ddd", CultureInfo.CurrentCulture),
                        TempLabel = $"{Math.Round((double)json["daily"]!["temperature_2m_max"]![i]!)}° / {Math.Round((double)json["daily"]!["temperature_2m_min"]![i]!)}°",
                        IconPath = $"/Assets/PNG/{MapCodeToPath(dailyCode, true, dailyWindSpeed)}.png",
                        Humidity = precipChance + "%",
                        Wind = Math.Round(dailyWindSpeed) + " mph"
                    });
                }
                return data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return null;
            }
        }

        public static async Task<bool> ValidateApiKeyAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return true; // Empty is valid for Open-Meteo (no key required)

            // For future API key validation if needed
            await Task.CompletedTask;
            return true;
        }

        private static string MapCodeToPath(int code, bool isDay, double windSpeed = 0)
        {
            bool isWindy = windSpeed > 25;
            string prefix = isDay ? "day" : "night";

            return code switch
            {
                // Clear sky
                0 => $"{prefix}_clear",

                // Mainly clear / Partly cloudy
                1 => isWindy ? $"{prefix}_partly_cloudy_wind" : $"{prefix}_partly_cloudy",
                2 => isWindy ? $"{prefix}_partly_cloudy_wind" : $"{prefix}_partly_cloudy",

                // Overcast
                3 => isWindy ? $"{prefix}_cloudy_wind" : $"{prefix}_cloudy",

                // Fog
                45 => $"{prefix}_partly_cloudy_fog",
                48 => $"{prefix}_partly_cloudy_fog",

                // Drizzle: Light, moderate, and dense
                51 => $"{prefix}_partly_cloudy_light_rain",
                53 => $"{prefix}_partly_cloudy_rain",
                55 => $"{prefix}_cloudy_rain",

                // Freezing Drizzle: Light and dense
                56 => $"{prefix}_cloudy_sleet",
                57 => $"{prefix}_cloudy_sleet",

                // Rain: Slight, moderate and heavy
                61 => $"{prefix}_partly_cloudy_light_rain",
                63 => $"{prefix}_cloudy_rain",
                65 => $"{prefix}_cloudy_heavy_rain",

                // Freezing Rain: Light and heavy
                66 => $"{prefix}_cloudy_sleet",
                67 => $"{prefix}_cloudy_heavy_rain_storm", // Heavy freezing rain

                // Snow fall: Slight, moderate, and heavy
                71 => $"{prefix}_partly_cloudy_light_snow",
                73 => $"{prefix}_cloudy_snow",
                75 => $"{prefix}_cloudy_snow_storm",

                // Snow grains
                77 => $"{prefix}_cloudy_snow",

                // Rain showers: Slight, moderate, and violent
                80 => $"{prefix}_partly_cloudy_light_rain",
                81 => $"{prefix}_cloudy_rain",
                82 => $"{prefix}_cloudy_heavy_rain",

                // Snow showers slight and heavy
                85 => $"{prefix}_partly_cloudy_light_snow",
                86 => $"{prefix}_cloudy_snow_storm",

                // Thunderstorm: Slight or moderate
                95 => $"{prefix}_partly_cloudy_rain_storm",

                // Thunderstorm with hail
                96 => $"{prefix}_cloudy_hail",
                99 => $"{prefix}_cloudy_hail",

                // Default fallback
                _ => $"{prefix}_cloudy"
            };
        }

        private static string MapCodeToString(int code) => code switch
        {
            0 => "Clear",
            1 => "Mostly Clear",
            2 => "Partly Cloudy",
            3 => "Overcast",
            45 or 48 => "Foggy",
            51 => "Light Drizzle",
            53 => "Drizzle",
            55 => "Dense Drizzle",
            56 or 57 => "Freezing Drizzle",
            61 => "Light Rain",
            63 => "Rain",
            65 => "Heavy Rain",
            66 or 67 => "Freezing Rain",
            71 => "Light Snow",
            73 => "Snow",
            75 => "Heavy Snow",
            77 => "Snow Grains",
            80 => "Light Showers",
            81 => "Showers",
            82 => "Heavy Showers",
            85 => "Light Snow Showers",
            86 => "Snow Showers",
            95 => "Thunderstorm",
            96 => "Hail",
            99 => "Heavy Hail",
            _ => "Cloudy"
        };
    }
}
