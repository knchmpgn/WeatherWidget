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
        public string? LastErrorMessage { get; set; }

        public async Task<LocationData> GetCurrentLocationAsync()
        {
            if (SettingsService.UseManualLocation)
            {
                LastErrorMessage = null;
                return new LocationData
                {
                    City = "Manual Location",
                    Latitude = SettingsService.ManualLatitude,
                    Longitude = SettingsService.ManualLongitude
                };
            }

            try
            {
                string response = await _http.GetStringAsync("https://ipapi.co/json/");
                var json = JObject.Parse(response);
                LastErrorMessage = null;
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
                LastErrorMessage = ex.Message;
                return new LocationData { City = "Munford, TN", Latitude = 35.44, Longitude = -89.81 };
            }
        }
    }
}
