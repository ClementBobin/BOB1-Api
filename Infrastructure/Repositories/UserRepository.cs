using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public UserRepository(AppDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(Guid id)
    {
        Log.Debug("GetByIdAsync {Id}", id);
        return await _db.Users.FindAsync(id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        Log.Debug("GetByEmailAsync {Email}", email);
        return await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        Log.Debug("GetAllAsync");
        return await _db.Users.AsNoTracking().ToListAsync();
    }

    public async Task AddAsync(User user)
    {
        user.Email = user.Email.ToLowerInvariant();
        Log.Info("AddAsync {Email}", user.Email);
        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        Log.Info("UpdateAsync {Id}", user.Id);
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        Log.Info("DeleteAsync {Id}", id);
        var user = await _db.Users.FindAsync(id);
        if (user is null) return;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ExistsByEmailAsync(string email)
        => await _db.Users.AnyAsync(u => u.Email == email.ToLowerInvariant());
}
