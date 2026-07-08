namespace Application.Interfaces;

using Domain.Dto;

public interface ITeamService
{
    Task<IEnumerable<TeamDto>> GetAllAsync();
    Task<IEnumerable<TeamDto>> GetByDivisionAsync(Guid divisionId);
    Task<TeamDto> GetByIdAsync(Guid id);
    Task<TeamDto> CreateAsync(CreateTeamRequest request);
    Task<TeamDto> UpdateAsync(Guid id, CreateTeamRequest request);
    Task DeleteAsync(Guid id);
}
