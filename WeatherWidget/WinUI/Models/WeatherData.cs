using System.Collections.Generic;

namespace WeatherWidget.Models
{
    public class WeatherData
    {
        public double Temperature { get; set; }
        public double HighTemp { get; set; }
        public double LowTemp { get; set; }
        public string Condition { get; set; } = "Unknown";
        public string IconCode { get; set; } = "cloudy";
        public List<ForecastItem> Hourly { get; set; } = [];
        public List<ForecastItem> Daily { get; set; } = [];

        public int CurrentHumidity { get; set; }
        public double CurrentWindSpeed { get; set; }
        public double FeelsLike { get; set; }
        public int PrecipitationChance { get; set; }
        public string Sunrise { get; set; } = "";
        public string Sunset { get; set; } = "";

        public double Pressure { get; set; }
        public int CloudCover { get; set; }
        public double Visibility { get; set; }
        public double UVIndex { get; set; }
    }

    public class ForecastItem
    {
        public string TimeLabel { get; set; } = "";
        public string TempLabel { get; set; } = "";
        public string IconPath { get; set; } = "";
        public string Humidity { get; set; } = "";
        public string Wind { get; set; } = "";
    }

    public class LocationData
    {
        public string City { get; set; } = "Munford, TN";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
