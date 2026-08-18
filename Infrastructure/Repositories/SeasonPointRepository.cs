namespace Infrastructure.Repositories;

using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using NLog;

public class SeasonPointRepository : ISeasonPointRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public SeasonPointRepository(AppDbContext db) => _db = db;

    public async Task<SeasonPoint?> GetByUserAndMatchAsync(Guid userId, Guid matchId)
    {
        _log.Debug("GetByUserAndMatchAsync user={UserId} match={MatchId}", userId, matchId);
        return await _db.SeasonPoints
            .FirstOrDefaultAsync(sp => sp.UserId == userId && sp.MatchId == matchId);
    }

    public async Task<IEnumerable<SeasonPoint>> GetByUserAsync(Guid userId, int? seasonId = null)
    {
        _log.Debug("GetByUserAsync user={UserId} season={SeasonId}", userId, seasonId);
        return await _db.SeasonPoints
            .AsNoTracking()
            .Where(sp => sp.UserId == userId && (seasonId == null || sp.SeasonId == seasonId))
            .OrderByDescending(sp => sp.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<SeasonPoint>> GetAllAsync(int? seasonId = null)
    {
        _log.Debug("GetAllAsync season={SeasonId}", seasonId);
        return await _db.SeasonPoints
            .AsNoTracking()
            .Where(sp => seasonId == null || sp.SeasonId == seasonId)
            .OrderByDescending(sp => sp.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(SeasonPoint point)
    {
        _log.Info("AddAsync user={UserId} match={MatchId} season={SeasonId} points={Points}",
            point.UserId, point.MatchId, point.SeasonId, point.Points);
        await _db.SeasonPoints.AddAsync(point);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(SeasonPoint point)
    {
        _log.Info("UpdateAsync {Id} points={Points}", point.Id, point.Points);
        point.UpdatedAt = DateTime.UtcNow;
        _db.SeasonPoints.Update(point);
        await _db.SaveChangesAsync();
    }
}