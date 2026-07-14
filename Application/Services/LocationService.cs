namespace Application.Services;

using Application.Interfaces;

using Domain.Dto;
using Domain.Entities;

using Infrastructure.Interfaces;

using NLog;

public class LocationService : ILocationService
{
    private readonly ILocationRepository _locations;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public LocationService(ILocationRepository locations) => _locations = locations;

    public async Task<IEnumerable<LocationDto>> GetAllAsync()
    {
        Log.Debug("GetAllAsync");
        return (await _locations.GetAllAsync()).Select(ToDto);
    }

    public async Task<LocationDto> GetByIdAsync(Guid id)
    {
        var loc = await _locations.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Location {id} not found.");
        return ToDto(loc);
    }

    public async Task<LocationDto> CreateAsync(CreateLocationRequest request)
    {
        Log.Info("CreateAsync {Name}", request.Name);
        var location = new Location
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Address = request.Address,
        };
        await _locations.AddAsync(location);
        return ToDto(location);
    }

    public async Task<LocationDto> UpdateAsync(Guid id, UpdateLocationRequest request)
    {
        Log.Info("UpdateAsync {Id}", id);
        var location = await _locations.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Location {id} not found.");

        location.Name = request.Name;
        location.Address = request.Address;

        await _locations.UpdateAsync(location);
        return ToDto(location);
    }

    public async Task DeleteAsync(Guid id)
    {
        Log.Info("DeleteAsync {Id}", id);
        await _locations.DeleteAsync(id);
    }

    private static LocationDto ToDto(Location l) =>
        new(l.Id, l.Name, l.Address,
            l.Coordinates?.Latitude, l.Coordinates?.Longitude,
            l.IsGeocoded, l.GeocodedAt);
}
