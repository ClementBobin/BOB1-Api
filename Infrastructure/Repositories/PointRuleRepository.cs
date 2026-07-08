using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Infrastructure.Repositories;

public class PointRuleRepository : IPointRuleRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public PointRuleRepository(AppDbContext db) => _db = db;

    public async Task<PointRule?> GetByIdAsync(Guid id)
    {
        Log.Debug("GetByIdAsync {Id}", id);
        return await _db.PointRules.FindAsync(id);
    }

    public async Task<PointRule?> GetByRoleAsync(OfficialRole role)
    {
        Log.Debug("GetByRoleAsync {Role}", role);
        return await _db.PointRules.FirstOrDefaultAsync(r => r.Role == role);
    }

    public async Task<IEnumerable<PointRule>> GetAllAsync()
    {
        Log.Debug("GetAllAsync");
        return await _db.PointRules
            .AsNoTracking()
            .OrderBy(r => r.Role)
            .ToListAsync();
    }

    public async Task AddAsync(PointRule rule)
    {
        Log.Info("AddAsync role={Role}", rule.Role);
        await _db.PointRules.AddAsync(rule);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(PointRule rule)
    {
        Log.Info("UpdateAsync {Id} role={Role}", rule.Id, rule.Role);
        _db.PointRules.Update(rule);
        await _db.SaveChangesAsync();
    }
}
