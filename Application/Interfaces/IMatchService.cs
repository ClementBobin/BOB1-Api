namespace Application.Interfaces;

using Domain.Dto;

public interface IMatchService
{
    Task<IEnumerable<MatchDto>> GetAllAsync(Guid userId);
    Task<IEnumerable<MatchDto>> GetByMonthAsync(int year, int month, Guid userId);
    Task<IEnumerable<MatchDto>> GetByDivisionAsync(Guid divisionId, Guid userId);
    Task<MatchDto> GetByIdAsync(Guid id, Guid userId);
    Task<MatchDto> CreateAsync(CreateMatchRequest request);
    Task DeleteAsync(Guid id);
    Task<MatchDto> SubscribeAsync(Guid matchId, Guid userId, SubscribeRequest request);
    Task<MatchDto> UnsubscribeAsync(Guid matchId, Guid userId);
    Task<MatchDto> ConfirmPresenceAsync(Guid matchId, Guid userId);
}
