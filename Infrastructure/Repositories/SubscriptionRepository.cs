using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Infrastructure.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public SubscriptionRepository(AppDbContext db) => _db = db;

    public async Task<Subscription?> GetAsync(Guid userId, Guid matchId)
    {
        Log.Debug("GetByUserAndMatchAsync user={UserId} match={MatchId}", userId, matchId);
        return await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MatchId == matchId);
    }

    public async Task<IEnumerable<Subscription>> GetByUserAsync(Guid userId)
    {
        Log.Debug("GetByUserAsync {UserId}", userId);
        return await _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Match)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Subscription>> GetByMatchAsync(Guid matchId)
    {
        Log.Debug("GetByMatchAsync {MatchId}", matchId);
        return await _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.MatchId == matchId)
            .ToListAsync();
    }

    public async Task AddAsync(Subscription subscription)
    {
        Log.Info("AddAsync user={UserId} match={MatchId} role={Role}",
            subscription.UserId, subscription.MatchId, subscription.Role);
        await _db.Subscriptions.AddAsync(subscription);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Subscription subscription)
    {
        Log.Info("UpdateAsync {Id} status={Status}", subscription.Id, subscription.Status);
        _db.Subscriptions.Update(subscription);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        Log.Info("DeleteAsync {Id}", id);
        var sub = await _db.Subscriptions.FindAsync(id);
        if (sub is null) return;
        _db.Subscriptions.Remove(sub);
        await _db.SaveChangesAsync();
    }
}
