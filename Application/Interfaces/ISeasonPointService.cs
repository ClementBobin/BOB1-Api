namespace Application.Interfaces;

using Domain.Dto;

public interface ISeasonPointService
{
    /// <summary>All entries for a single user, with total.</summary>
    Task<SeasonPointSummaryDto> GetByUserAsync(Guid userId);

    /// <summary>All entries for all users (admin view).</summary>
    Task<IEnumerable<SeasonPointDto>> GetAllAsync();
}