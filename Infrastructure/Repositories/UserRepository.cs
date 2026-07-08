namespace Infrastructure.Repositories;

using Domain.Entities;

using Infrastructure.Data;
using Infrastructure.Interfaces;

using Microsoft.EntityFrameworkCore;
using NLog;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public UserRepository(AppDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(Guid id)
    {
        _log.Debug("GetByIdAsync {Id}", id);
        return await _db.Users.FindAsync(id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        _log.Debug("GetByEmailAsync {Email}", email);
        return await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        _log.Debug("GetAllAsync");
        return await _db.Users.AsNoTracking().ToListAsync();
    }

    public async Task AddAsync(User user)
    {
        user.Email = user.Email.ToLowerInvariant();
        _log.Info("AddAsync {Email}", user.Email);
        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _log.Info("UpdateAsync {Id}", user.Id);
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        _log.Info("DeleteAsync {Id}", id);
        var user = await _db.Users.FindAsync(id);
        if (user is null) return;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ExistsByEmailAsync(string email)
        => await _db.Users.AnyAsync(u => u.Email == email.ToLowerInvariant());
}
