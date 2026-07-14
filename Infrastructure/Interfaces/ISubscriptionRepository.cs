namespace Infrastructure.Interfaces;

using Domain.Entities;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetAsync(Guid userId, Guid matchId);
    Task<IEnumerable<Subscription>> GetByMatchAsync(Guid matchId);
    Task<IEnumerable<Subscription>> GetByUserAsync(Guid userId);
    Task AddAsync(Subscription subscription);
    Task UpdateAsync(Subscription subscription);
    Task DeleteAsync(Guid id);
    Task<IEnumerable<Subscription>> GetByUserAndMatchesAsync(Guid userId, IEnumerable<Guid> matchIds);
}
