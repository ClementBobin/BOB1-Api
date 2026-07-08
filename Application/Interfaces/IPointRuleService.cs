namespace Application.Interfaces;

using Domain.Dto;

public interface IPointRuleService
{
    Task<IEnumerable<PointRuleDto>> GetAllAsync();
    Task<PointRuleDto> UpdateAsync(Guid id, UpdatePointRuleRequest request);
}
