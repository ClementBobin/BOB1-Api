namespace Infrastructure.Repositories;

using Domain.Entities;
using Domain.Enums;

using Infrastructure.Data;
using Infrastructure.Interfaces;

using Microsoft.EntityFrameworkCore;
using NLog;

public class PointRuleRepository : IPointRuleRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public PointRuleRepository(AppDbContext db) => _db = db;

    public async Task<PointRule?> GetByIdAsync(Guid id)
    {
        _log.Debug("GetByIdAsync {Id}", id);
        return await _db.PointRules.FindAsync(id);
    }

    public async Task<PointRule?> GetByRoleAsync(OfficialRole role)
    {
        _log.Debug("GetByRoleAsync {Role}", role);
        return await _db.PointRules.FirstOrDefaultAsync(r => r.Role == role);
    }

    public async Task<IEnumerable<PointRule>> GetAllAsync()
    {
        _log.Debug("GetAllAsync");
        return await _db.PointRules
            .AsNoTracking()
            .OrderBy(r => r.Role)
            .ToListAsync();
    }

    public async Task AddAsync(PointRule rule)
    {
        _log.Info("AddAsync role={Role}", rule.Role);
        await _db.PointRules.AddAsync(rule);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(PointRule rule)
    {
        _log.Info("UpdateAsync {Id} role={Role}", rule.Id, rule.Role);
        _db.PointRules.Update(rule);
        await _db.SaveChangesAsync();
    }
}
