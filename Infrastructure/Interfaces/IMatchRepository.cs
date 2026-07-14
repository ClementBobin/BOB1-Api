namespace Infrastructure.Interfaces;

using Domain.Entities;

public interface IMatchRepository
{
    Task<Match?> GetByIdAsync(Guid id);
    Task<IEnumerable<Match>> GetAllAsync();
    Task<IEnumerable<Match>> GetByMonthAsync(int year, int month);
    Task<IEnumerable<Match>> GetByDivisionAsync(Guid divisionId);
    Task AddAsync(Match match);
    Task UpdateAsync(Match match);
    Task UpdateSlotAsync(RoleSlot slot);
    Task DeleteAsync(Guid id);
}