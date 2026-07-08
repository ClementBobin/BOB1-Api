namespace Infrastructure.Interfaces;

using Domain.Entities;

public interface IPenaltyRepository
{
    Task<Penalty?> GetByIdAsync(Guid id);
    Task<IEnumerable<Penalty>> GetByUserAsync(Guid userId);
    Task<IEnumerable<Penalty>> GetAllAsync();
    Task AddAsync(Penalty penalty);
    Task UpdateAsync(Penalty penalty);
    Task DeleteAsync(Guid id);
}
