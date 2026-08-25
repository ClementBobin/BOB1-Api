namespace Infrastructure.Interfaces;

using System.Security.Claims;

using Domain.Dto;

/// <summary>
/// Génère des tokens JWT pour un ensemble de claims donné.
/// </summary>
public interface ITokenGenerator
{
    /// <summary>
    /// Génère un token JWT signé encodant les claims fournis.
    /// </summary>
    /// <param name="claims">Les claims à inclure dans le token (id utilisateur, rôle, ...).</param>
    /// <returns>Le token JWT encodé.</returns>
    LoginResponse GenerateToken(IEnumerable<Claim> claims);
}
