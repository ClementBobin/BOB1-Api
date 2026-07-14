namespace Application.Interfaces;

using Domain.Dto;
using Domain.Enums;

public interface IUserAdminService
{
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task<UserDto> GetByIdAsync(Guid id);
    Task<UserDto> ChangeRoleAsync(Guid userId, UserRole newRole);
}
