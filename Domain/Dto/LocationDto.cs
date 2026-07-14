namespace Domain.Dto;

public record LocationDto(
    Guid Id,
    string Name,
    string Address,
    double? Latitude,
    double? Longitude,
    bool IsGeocoded,
    DateTime? GeocodedAt);

public record CreateLocationRequest(
    string Name,
    string Address);

public record UpdateLocationRequest(
    string Name,
    string Address);
