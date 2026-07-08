namespace Application.Interfaces;

public interface IGeocodingService
{
    Task<(double Latitude, double Longitude)?> GeocodeAddressAsync(string address);
}