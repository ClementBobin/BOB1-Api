namespace Application.Services;

using Interfaces;

using Domain.Dto;
using Domain.Entities;

using Infrastructure.Interfaces;

using NLog;

public class TeamService : ITeamService
{
    private readonly ITeamRepository _teams;
    private readonly IDivisionRepository _divisions;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public TeamService(ITeamRepository teams, IDivisionRepository divisions)
    {
        _teams = teams;
        _divisions = divisions;
    }

    public async Task<IEnumerable<TeamDto>> GetAllAsync()
        => (await _teams.GetAllAsync()).Select(ToDto);

    public async Task<IEnumerable<TeamDto>> GetByDivisionAsync(Guid divisionId)
        => (await _teams.GetByDivisionAsync(divisionId)).Select(ToDto);

    public async Task<TeamDto> GetByIdAsync(Guid id)
    {
        var team = await _teams.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Team {id} not found.");
        return ToDto(team);
    }

    public async Task<TeamDto> CreateAsync(CreateTeamRequest request)
    {
        Log.Info("Creating team {Name} in division {DivisionId}", request.Name, request.DivisionId);

        _ = await _divisions.GetByIdAsync(request.DivisionId)
            ?? throw new KeyNotFoundException($"Division {request.DivisionId} not found.");

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            DivisionId = request.DivisionId,
            LogoUrl = request.LogoUrl,
        };

        await _teams.AddAsync(team);

        return ToDto(await _teams.GetByIdAsync(team.Id));
    }

    public async Task<TeamDto> UpdateAsync(Guid id, CreateTeamRequest request)
    {
        Log.Info("Updating team {Id}", id);

        var team = await _teams.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Team {id} not found.");

        team.Name = request.Name;
        team.DivisionId = request.DivisionId;
        team.LogoUrl = request.LogoUrl;

        await _teams.UpdateAsync(team);
        return ToDto(await _teams.GetByIdAsync(team.Id));
    }

    public async Task DeleteAsync(Guid id)
    {
        Log.Info("Deleting team {Id}", id);
        await _teams.DeleteAsync(id);
    }

    private static TeamDto ToDto(Team? t)
    {
        return new(t.Id, t.Name, t.LogoUrl, new DivisionDto(t.Division.Id, t.Division.Name));
    }
}
