namespace Infrastructure.Interfaces;

using Domain.Entities;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id);
    Task<IEnumerable<Team>> GetAllAsync();
    Task<IEnumerable<Team>> GetByDivisionAsync(Guid divisionId);
    Task AddAsync(Team team);
    Task UpdateAsync(Team team);
    Task DeleteAsync(Guid id);
}
