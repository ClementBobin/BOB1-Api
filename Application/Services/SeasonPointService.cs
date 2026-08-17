namespace Application.Services;

using Application.Interfaces;
using Domain.Dto;
using Infrastructure.Interfaces;
using NLog;
using Tools;

public class SeasonPointService : ISeasonPointService
{
    private readonly ISeasonPointRepository _seasonPoints;
    private readonly IPenaltyRepository _penalties;
    private readonly IUserRepository _users;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public SeasonPointService(
        ISeasonPointRepository seasonPoints,
        IPenaltyRepository penalties,
        IUserRepository users)
    {
        _seasonPoints = seasonPoints;
        _penalties = penalties;
        _users = users;
    }

    public async Task<SeasonPointSummaryDto> GetByUserAsync(Guid userId, int? seasonId = null)
    {
        var season = seasonId ?? SeasonHelper.CurrentSeasonId;
        _log.Debug("GetByUserAsync user={UserId} season={SeasonId}", userId, season);

        var entriesTask = _seasonPoints.GetByUserAsync(userId, season);
        var penaltiesTask = _penalties.GetByUserAsync(userId, season);
        await Task.WhenAll(entriesTask, penaltiesTask);

        var dtos = entriesTask.Result.Select(ToDto).ToList();
        var penaltyDeductions = penaltiesTask.Result.Sum(p => p.Points);
        var total = dtos.Sum(d => d.Points) - penaltyDeductions;

        return new SeasonPointSummaryDto(season, userId, total, dtos);
    }

    public async Task<IEnumerable<SeasonPointRankingDto>> GetRankingAsync(int? seasonId = null)
    {
        var season = seasonId ?? SeasonHelper.CurrentSeasonId;
        _log.Debug("GetRankingAsync season={SeasonId}", season);

        var usersTask = _users.GetAllAsync();
        var pointsTask = _seasonPoints.GetAllAsync(season);
        var penaltiesTask = _penalties.GetAllAsync(season);
        await Task.WhenAll(usersTask, pointsTask, penaltiesTask);

        var earnedByUser = usersTask.Result
            .ToDictionary(
                u => u.Id,
                u => pointsTask.Result
                        .Where(sp => sp.UserId == u.Id)
                        .Sum(sp => sp.Points)
                     - penaltiesTask.Result
                        .Where(p => p.UserId == u.Id)
                        .Sum(p => p.Points));

        return earnedByUser
            .OrderByDescending(kv => kv.Value)
            .Select((kv, index) =>
            {
                var user = usersTask.Result.First(u => u.Id == kv.Key);
                return new SeasonPointRankingDto(
                    index + 1,
                    user.Id,
                    $"{user.FirstName} {user.LastName}",
                    kv.Value);
            });
    }

    private static SeasonPointDto ToDto(Domain.Entities.SeasonPoint sp) =>
        new(sp.Id, sp.SeasonId, sp.UserId, sp.MatchId, sp.Points, sp.CreatedAt, sp.UpdatedAt);
}