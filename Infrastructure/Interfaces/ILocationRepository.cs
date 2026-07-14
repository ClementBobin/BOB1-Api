namespace Infrastructure.Interfaces;

using Domain.Entities;

public interface ILocationRepository
{
    Task<IEnumerable<Location>> GetAllAsync();
    Task<Location?> GetByIdAsync(Guid id);
    Task AddAsync(Location location);
    Task UpdateAsync(Location location);
    Task DeleteAsync(Guid id);
}
