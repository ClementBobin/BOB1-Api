namespace Application.Services;

using Application.Interfaces;

using Domain.Dto;

using Infrastructure.Interfaces;

using NLog;

public class SeasonPointService : ISeasonPointService
{
    private readonly ISeasonPointRepository _seasonPoints;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public SeasonPointService(ISeasonPointRepository seasonPoints) => _seasonPoints = seasonPoints;

    public async Task<SeasonPointSummaryDto> GetByUserAsync(Guid userId)
    {
        _log.Debug("GetByUserAsync {UserId}", userId);
        var entries = (await _seasonPoints.GetByUserAsync(userId)).ToList();
        var dtos = entries.Select(ToDto).ToList();
        return new SeasonPointSummaryDto(userId, dtos.Sum(d => d.Points), dtos);
    }

    public async Task<IEnumerable<SeasonPointDto>> GetAllAsync()
    {
        _log.Debug("GetAllAsync");
        return (await _seasonPoints.GetAllAsync()).Select(ToDto);
    }

    private static SeasonPointDto ToDto(Domain.Entities.SeasonPoint sp) =>
        new(sp.Id, sp.UserId, sp.MatchId, sp.Points, sp.CreatedAt, sp.UpdatedAt);
}