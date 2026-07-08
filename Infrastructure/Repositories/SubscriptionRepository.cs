namespace Infrastructure.Repositories;

using Domain.Entities;

using Infrastructure.Data;
using Infrastructure.Interfaces;

using Microsoft.EntityFrameworkCore;
using NLog;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public SubscriptionRepository(AppDbContext db) => _db = db;

    public async Task<Subscription?> GetAsync(Guid userId, Guid matchId)
    {
        _log.Debug("GetByUserAndMatchAsync user={UserId} match={MatchId}", userId, matchId);
        return await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MatchId == matchId);
    }

    public async Task<IEnumerable<Subscription>> GetByUserAsync(Guid userId)
    {
        _log.Debug("GetByUserAsync {UserId}", userId);
        return await _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Match)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Subscription>> GetByMatchAsync(Guid matchId)
    {
        _log.Debug("GetByMatchAsync {MatchId}", matchId);
        return await _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.MatchId == matchId)
            .ToListAsync();
    }

    public async Task AddAsync(Subscription subscription)
    {
        _log.Info("AddAsync user={UserId} match={MatchId} role={Role}",
            subscription.UserId, subscription.MatchId, subscription.Role);
        await _db.Subscriptions.AddAsync(subscription);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Subscription subscription)
    {
        _log.Info("UpdateAsync {Id} status={Status}", subscription.Id, subscription.Status);
        _db.Subscriptions.Update(subscription);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        _log.Info("DeleteAsync {Id}", id);
        var sub = await _db.Subscriptions.FindAsync(id);
        if (sub is null) return;
        _db.Subscriptions.Remove(sub);
        await _db.SaveChangesAsync();
    }
}
