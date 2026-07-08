namespace Application.Services;

using System.Security.Claims;

using Interfaces;

using Domain.Dto;
using Domain.Entities;
using Domain.Enums;

using Infrastructure.Interfaces;

using NLog;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly ITokenGenerator _tokens;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public AuthService(IUserRepository users, ITokenGenerator tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        Log.Info("Login attempt for {Email}", request.Email);

        var user = await _users.GetByEmailAsync(request.Email)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var token = GenerateToken(user);
        Log.Info("Login successful for {Email}", user.Email);
        return new LoginResponse(token, ToDto(user));
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
    {
        Log.Info("Register {Email}", request.Email);

        if (await _users.ExistsByEmailAsync(request.Email))
            throw new InvalidOperationException($"Email '{request.Email}' is already taken.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.ToLowerInvariant(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Official,
        };

        await _users.AddAsync(user);
        var token = GenerateToken(user);
        return new LoginResponse(token, ToDto(user));
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");
        return ToDto(user);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };
        return _tokens.GenerateToken(claims);
    }

    private static UserDto ToDto(User u) =>
        new(u.Id, u.Email, u.FirstName, u.LastName, u.Role);
}
