namespace Application.Interfaces;

using Domain.Dto;

public interface IDivisionService
{
    Task<IEnumerable<DivisionDto>> GetAllAsync();
}
