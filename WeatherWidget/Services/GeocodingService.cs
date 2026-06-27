using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WeatherWidget.Models;

namespace WeatherWidget.Services
{
    public class GeocodingService
    {
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

        // Open-Meteo geocoding — free, no API key required.
        private const string BaseUrl = "https://geocoding-api.open-meteo.com/v1/search";

        public async Task<List<LocationSuggestion>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            var results = new List<LocationSuggestion>();

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return results;

            string url = $"{BaseUrl}?name={Uri.EscapeDataString(query.Trim())}&count=8&language=en&format=json";

            string json = await _http.GetStringAsync(url, cancellationToken);
            var root = JObject.Parse(json);

            var items = root["results"] as JArray;
            if (items == null)
                return results;

            foreach (var item in items)
            {
                double? lat = item["latitude"]?.Value<double>();
                double? lon = item["longitude"]?.Value<double>();
                if (lat == null || lon == null)
                    continue;

                string name = item["name"]?.Value<string>() ?? "";
                string admin = item["admin1"]?.Value<string>() ?? "";
                string country = item["country_code"]?.Value<string>() ?? "";

                string display = string.IsNullOrEmpty(admin)
                    ? $"{name}, {country}"
                    : $"{name}, {admin}, {country}";

                results.Add(new LocationSuggestion
                {
                    DisplayName = display,
                    Latitude = lat.Value,
                    Longitude = lon.Value
                });
            }

            return results;
        }
    }
}
