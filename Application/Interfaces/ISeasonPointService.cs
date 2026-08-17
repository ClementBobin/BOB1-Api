namespace Application.Interfaces;

using Domain.Dto;

public interface ISeasonPointService
{
    Task<SeasonPointSummaryDto> GetByUserAsync(Guid userId, int? seasonId = null);
    Task<IEnumerable<SeasonPointRankingDto>> GetRankingAsync(int? seasonId = null);
}