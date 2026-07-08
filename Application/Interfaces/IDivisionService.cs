namespace Application.Interfaces;

using Domain.Dto;

public interface IDivisionService
{
    Task<IEnumerable<DivisionDto>> GetAllAsync();
    Task<DivisionDto> CreateAsync(CreateDivisionRequest request);
    Task DeleteAsync(Guid id);
}
