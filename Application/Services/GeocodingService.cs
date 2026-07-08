using System.Text.Json;
using Application.Interfaces;
using NLog;

namespace Application.Services;

public class GeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public GeocodingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "YourAppName/1.0");
    }

    public async Task<(double Latitude, double Longitude)?> GeocodeAddressAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            Log.Warn("Empty address provided for geocoding");
            return null;
        }

        try
        {
            var encodedAddress = Uri.EscapeDataString(address);
            var url = $"https://nominatim.openstreetmap.org/search?q={encodedAddress}&format=json&limit=1";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warn("Geocoding API returned {StatusCode} for address: {Address}",
                    response.StatusCode, address);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var results = JsonSerializer.Deserialize<NominatimResult[]>(json);

            if (results?.Length > 0)
            {
                var result = results[0];
                if (double.TryParse(result.Lat, out var lat) && double.TryParse(result.Lon, out var lon))
                {
                    return (lat, lon);
                }
            }

            Log.Info("No geocoding results found for address: {Address}", address);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error geocoding address: {Address}", address);
            return null;
        }
    }
}

public class NominatimResult
{
    public string Lat { get; set; } = string.Empty;
    public string Lon { get; set; } = string.Empty;
}