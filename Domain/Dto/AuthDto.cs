namespace Domain.Dto;

using Domain.Enums;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, UserDto User);

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName);

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    UserRole Role);
