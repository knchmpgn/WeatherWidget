using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WeatherWidget.Models;

namespace WeatherWidget.Services
{
    public class WeatherService
    {
        private readonly HttpClient _http = new();

        public async Task<WeatherData?> GetWeatherDataAsync(double lat, double lon)
        {
            try
            {
                string url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code,is_day,wind_speed_10m,relative_humidity_2m,apparent_temperature&hourly=temperature_2m,weather_code,wind_speed_10m,is_day&daily=sunrise,sunset,temperature_2m_max,temperature_2m_min,weather_code,wind_speed_10m_max,precipitation_probability_max&temperature_unit=fahrenheit&timezone=auto&wind_speed_unit=mph";
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
                    Sunset = (string)json["daily"]!["sunset"]![0]!
                };

                // Hourly Forecast (5 items)
                for (int i = 0; i < 5; i++)
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
                // Use current time to determine if we should show day or night icons for ALL forecast cards
                DateTime now = DateTime.Now;

                // Get today's sunrise/sunset to determine current day/night status
                string todaySunrise = (string)json["daily"]!["sunrise"]![0]!;
                string todaySunset = (string)json["daily"]!["sunset"]![0]!;
                DateTime sunrise = DateTime.Parse(todaySunrise);
                DateTime sunset = DateTime.Parse(todaySunset);
                bool isDayTime = now >= sunrise && now < sunset;

                for (int i = 1; i <= 5; i++)
                {
                    var time = DateTime.Parse((string)json["daily"]!["time"]![i]!);
                    int dailyCode = (int)json["daily"]!["weather_code"]![i]!;
                    double dailyWindSpeed = (double)json["daily"]!["wind_speed_10m_max"]![i]!;
                    int precipChance = json["daily"]!["precipitation_probability_max"]![i] != null
                        ? (int)json["daily"]!["precipitation_probability_max"]![i]!
                        : 0;

                    // Use the same day/night status for all forecast cards based on current time
                    data.Daily.Add(new ForecastItem
                    {
                        TimeLabel = time.ToString("ddd", CultureInfo.CurrentCulture),
                        TempLabel = $"{Math.Round((double)json["daily"]!["temperature_2m_max"]![i]!)}° / {Math.Round((double)json["daily"]!["temperature_2m_min"]![i]!)}°",
                        IconPath = $"/Assets/PNG/{MapCodeToPath(dailyCode, isDayTime, dailyWindSpeed)}.png",
                        Humidity = precipChance + "%",
                        Wind = Math.Round(dailyWindSpeed) + " mph"
                    });
                }
                return data;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> ValidateApiKeyAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return true; // Empty is valid for Open-Meteo (no key required)

            // For future API key validation if needed
            return true;
        }

        private string MapCodeToPath(int code, bool isDay, double windSpeed = 0)
        {
            bool isWindy = windSpeed > 25;
            string prefix = isDay ? "day" : "night";

            return code switch
            {
                0 => $"{prefix}-clear",
                1 or 2 => isWindy ? $"{prefix}-partly-cloudy-wind" : $"{prefix}-partly-cloudy",
                3 => isWindy ? $"{prefix}-cloudy-wind" : $"{prefix}-cloudy",
                45 or 48 => $"{prefix}-clear-fog",
                51 or 53 or 55 => $"{prefix}-partly-cloudy-rain",
                56 or 57 => $"{prefix}-cloudy-sleet",
                61 => $"{prefix}-partly-cloudy-rain",
                63 or 65 => $"{prefix}-cloudy-rain",
                66 or 67 => $"{prefix}-cloudy-sleet",
                71 => $"{prefix}-partly-cloudy-snow",
                73 => $"{prefix}-cloudy-snow",
                75 => $"{prefix}-cloudy-snow-storm",
                77 => $"{prefix}-cloudy-snow-storm",
                80 => $"{prefix}-partly-cloudy-rain",
                81 or 82 => $"{prefix}-cloudy-rain",
                85 => $"{prefix}-partly-cloudy-snow",
                86 => $"{prefix}-cloudy-snow-storm",
                95 => $"{prefix}-cloudy-lightning",
                96 or 99 => $"{prefix}-cloudy-hail",
                _ => $"{prefix}-cloudy"
            };
        }

        private string MapCodeToString(int code) => code switch
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