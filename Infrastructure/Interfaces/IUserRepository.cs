namespace Infrastructure.Interfaces;

using Domain.Entities;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllAsync();
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsByEmailAsync(string email); // used by AuthService
    Task<User?> GetByBiometricTokenAsync(string token); // used by AuthService
    Task GenerateBiometricTokenAsync(User user); // used by AuthService
    Task RemoveBiometricTokenAsync(User user); // used by AuthService
}
