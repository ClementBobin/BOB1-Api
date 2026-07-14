namespace Application.Interfaces;

using Domain.Dto;

public interface ILocationService
{
    Task<IEnumerable<LocationDto>> GetAllAsync();
    Task<LocationDto> GetByIdAsync(Guid id);
    Task<LocationDto> CreateAsync(CreateLocationRequest request);
    Task<LocationDto> UpdateAsync(Guid id, UpdateLocationRequest request);
    Task DeleteAsync(Guid id);
}
