using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WeatherWidget.Models;

namespace WeatherWidget.Services
{
    public class LocationService
    {
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

        public async Task<LocationData> GetCurrentLocationAsync()
        {
            // Check if manual location is enabled
            var settings = Properties.Settings.Default;
            if (settings.UseManualLocation)
            {
                return new LocationData
                {
                    City = "Manual Location",
                    Latitude = settings.ManualLatitude,
                    Longitude = settings.ManualLongitude
                };
            }

            // Otherwise use IP-based detection
            try
            {
                string response = await _http.GetStringAsync("https://ipapi.co/json/");
                var json = JObject.Parse(response);
                return new LocationData
                {
                    City = $"{json["city"]}, {json["region_code"]}",
                    Latitude = (double)json["latitude"]!,
                    Longitude = (double)json["longitude"]!
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return new LocationData { City = "Munford, TN", Latitude = 35.44, Longitude = -89.81 };
            }
        }
    }
}
