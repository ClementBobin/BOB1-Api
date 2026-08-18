namespace Infrastructure.Interfaces;

using Domain.Entities;

public interface ISeasonPointRepository
{
    Task<SeasonPoint?> GetByUserAndMatchAsync(Guid userId, Guid matchId);
    Task<IEnumerable<SeasonPoint>> GetByUserAsync(Guid userId, int? seasonId = null);
    Task<IEnumerable<SeasonPoint>> GetAllAsync(int? seasonId = null);
    Task AddAsync(SeasonPoint point);
    Task UpdateAsync(SeasonPoint point);
}