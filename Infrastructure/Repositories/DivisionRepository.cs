namespace Infrastructure.Repositories;

using Domain.Entities;

using Infrastructure.Data;
using Infrastructure.Interfaces;

using Microsoft.EntityFrameworkCore;
using NLog;

public class DivisionRepository : IDivisionRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public DivisionRepository(AppDbContext db) => _db = db;

    public async Task<Division?> GetByIdAsync(Guid id)
    {
        _log.Debug("GetByIdAsync {Id}", id);
        return await _db.Divisions.FindAsync(id);
    }

    public async Task<bool> ExistsByNameAsync(string name)
        => await _db.Divisions.AnyAsync(d => d.Name.ToLower() == name.ToLowerInvariant());

    public async Task<IEnumerable<Division>> GetAllAsync()
    {
        _log.Debug("GetAllAsync");
        return await _db.Divisions.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
    }

    public async Task AddAsync(Division division)
    {
        _log.Info("AddAsync {Name}", division.Name);
        await _db.Divisions.AddAsync(division);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        _log.Info("DeleteAsync {Id}", id);
        var match = await _db.Matches.FindAsync(id);
        if (match is null) return;
        _db.Matches.Remove(match);
        await _db.SaveChangesAsync();
    }
}
