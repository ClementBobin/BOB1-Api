namespace Application.Interfaces;

using Domain.Dto;

public interface IPenaltyService
{
    Task<IEnumerable<PenaltyDto>> GetByUserAsync(Guid userId);
    Task<IEnumerable<PenaltyDto>> GetAllAsync();
    Task<PenaltyDto> CreateAsync(CreatePenaltyRequest request);
    Task AcknowledgeAsync(Guid penaltyId, Guid userId);
}
