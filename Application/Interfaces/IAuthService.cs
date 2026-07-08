namespace Application.Interfaces;

using Domain.Entities;
using Domain.Dto;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginResponse> RegisterAsync(RegisterRequest request);
    Task<UserDto> GetCurrentUserAsync(Guid userId);
}
