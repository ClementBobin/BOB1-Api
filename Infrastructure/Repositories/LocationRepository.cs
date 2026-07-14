namespace Infrastructure.Repositories;

using Domain.Entities;

using Infrastructure.Data;
using Infrastructure.Interfaces;

using Microsoft.EntityFrameworkCore;

using NLog;

public class LocationRepository : ILocationRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public LocationRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Location>> GetAllAsync()
    {
        Log.Debug("GetAllAsync");
        return await _db.Locations.AsNoTracking().OrderBy(l => l.Name).ToListAsync();
    }

    public async Task<Location?> GetByIdAsync(Guid id)
    {
        Log.Debug("GetByIdAsync {Id}", id);
        return await _db.Locations.FindAsync(id);
    }

    public async Task AddAsync(Location location)
    {
        Log.Info("AddAsync {Name}", location.Name);
        await _db.Locations.AddAsync(location);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Location location)
    {
        Log.Info("UpdateAsync {Id}", location.Id);
        _db.Locations.Update(location);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        Log.Info("DeleteAsync {Id}", id);
        var location = await _db.Locations.FindAsync(id);
        if (location is null) return;
        _db.Locations.Remove(location);
        await _db.SaveChangesAsync();
    }
}
