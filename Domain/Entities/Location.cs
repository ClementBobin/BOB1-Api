using Domain.ValueObjects;

namespace Domain.Entities;

public class Location
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    // Nullable — geocoding may fail or not be configured
    public Coordinates? Coordinates { get; set; }
    public bool IsGeocoded => Coordinates != null;
    public DateTime? GeocodedAt { get; set; }

    public ICollection<Match> Matches { get; set; } = [];
}
