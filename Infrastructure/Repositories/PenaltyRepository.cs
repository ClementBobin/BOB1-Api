namespace Infrastructure.Repositories;

using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using NLog;

public class PenaltyRepository : IPenaltyRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public PenaltyRepository(AppDbContext db) => _db = db;

    public async Task<Penalty?> GetByIdAsync(Guid id)
    {
        _log.Debug("GetByIdAsync {Id}", id);
        return await _db.Penalties
            .Include(p => p.User)
            .Include(p => p.Match)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Penalty>> GetByUserAsync(Guid userId, int? seasonId = null)
    {
        _log.Debug("GetByUserAsync user={UserId} season={SeasonId}", userId, seasonId);
        return await _db.Penalties
            .AsNoTracking()
            .Include(p => p.Match)
            .Where(p => p.UserId == userId && (seasonId == null || p.SeasonId == seasonId))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Penalty>> GetAllAsync(int? seasonId = null)
    {
        _log.Debug("GetAllAsync season={SeasonId}", seasonId);
        return await _db.Penalties
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.Match)
            .Where(p => seasonId == null || p.SeasonId == seasonId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Penalty penalty)
    {
        _log.Info("AddAsync user={UserId} season={SeasonId} points={Points}",
            penalty.UserId, penalty.SeasonId, penalty.Points);
        await _db.Penalties.AddAsync(penalty);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Penalty penalty)
    {
        _log.Info("UpdateAsync {Id}", penalty.Id);
        _db.Penalties.Update(penalty);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        _log.Info("DeleteAsync {Id}", id);
        var penalty = await _db.Penalties.FindAsync(id);
        if (penalty is null) return;
        _db.Penalties.Remove(penalty);
        await _db.SaveChangesAsync();
    }
}