namespace Infrastructure.Interfaces;

using Domain.Entities;
using Domain.Enums;

public interface IPointRuleRepository
{
    Task<PointRule?> GetByIdAsync(Guid id);
    Task<PointRule?> GetByRoleAsync(OfficialRole role);
    Task<IEnumerable<PointRule>> GetAllAsync();
    Task AddAsync(PointRule rule);
    Task UpdateAsync(PointRule rule);
}
