namespace Domain.Dto;

public record LocationDto(
    Guid Id,
    string Name,
    string Address,
    double? Latitude,
    double? Longitude);

public record CreateLocationRequest(
    string Name,
    string Address);

public record UpdateLocationRequest(
    string Name,
    string Address);
