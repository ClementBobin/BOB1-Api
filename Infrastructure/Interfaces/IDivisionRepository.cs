namespace Infrastructure.Interfaces;

using Domain.Entities;

public interface IDivisionRepository
{
    Task<IEnumerable<Division>> GetAllAsync();
    Task<Division?> GetByIdAsync(Guid id);
    Task<bool> ExistsByNameAsync(string name);
    Task AddAsync(Division division);
    Task DeleteAsync(Guid id);
}
