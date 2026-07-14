namespace Application.Services;

using Application.Interfaces;

using Domain.Dto;
using Domain.Enums;

using Infrastructure.Interfaces;

using NLog;

public class UserAdminService : IUserAdminService
{
    private readonly IUserRepository _users;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public UserAdminService(IUserRepository users) => _users = users;

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        Log.Debug("GetAllAsync");
        return (await _users.GetAllAsync()).Select(ToDto);
    }

    public async Task<UserDto> GetByIdAsync(Guid id)
    {
        var user = await _users.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found.");
        return ToDto(user);
    }

    public async Task<UserDto> ChangeRoleAsync(Guid userId, UserRole newRole)
    {
        Log.Info("ChangeRoleAsync {UserId} → {Role}", userId, newRole);
        var user = await _users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        user.Role = newRole;
        await _users.UpdateAsync(user);
        return ToDto(user);
    }

    private static UserDto ToDto(Domain.Entities.User u) =>
        new(u.Id, u.Email, u.FirstName, u.LastName, u.Role);
}
