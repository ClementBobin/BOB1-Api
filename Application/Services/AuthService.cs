namespace Application.Services;

using System.Security.Claims;
using Application.Interfaces;
using Domain.Dto;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Interfaces;
using NLog;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly ITokenGenerator _tokens;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public AuthService(IUserRepository users, ITokenGenerator tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        _log.Info("Login attempt for {Email}", request.Email);

        var user = await _users.GetByEmailAsync(request.Email)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var token = GenerateToken(user);
        _log.Info("Login successful for {Email}", user.Email);
        return token;
    }

    public async Task<UserDto> RegisterAsync(RegisterRequest request)
    {
        _log.Info("Register {Email}", request.Email);

        if (await _users.ExistsByEmailAsync(request.Email))
            throw new InvalidOperationException($"Email '{request.Email}' is already taken.");

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = request.Email.ToLowerInvariant(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Roles = new List<UserRoleMapping>
            {
                new UserRoleMapping { UserId = userId, Role = UserRole.Official }
            }
        };

        await _users.AddAsync(user);
        return ToDto(user);
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");
        return ToDto(user);
    }

    public async Task<LoginResponse> GenerateBiometricTokenAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        return await _users.GenerateBiometricTokenAsync(user);
    }

    public async Task RemoveBiometricTokenAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        await _users.RemoveBiometricTokenAsync(user);
    }

    public async Task<LoginResponse> LoginWithBiometricTokenAsync(string bioToken)
    {
        var user = await _users.GetByBiometricTokenAsync(bioToken)
            ?? throw new UnauthorizedAccessException("Invalid biometric token.");

        var token = GenerateToken(user);
        return token;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private LoginResponse GenerateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
        };

        foreach (var roleMapping in user.Roles)
        {
            claims.Add(new(ClaimTypes.Role, roleMapping.Role.ToString()));
        }

        return _tokens.GenerateToken(claims);
    }

    private static UserDto ToDto(User u) =>
        new(u.Id, u.Email, u.FirstName, u.LastName, u.Roles.Select(r => r.Role).ToList());
}