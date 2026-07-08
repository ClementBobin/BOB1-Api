using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Infrastructure.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public TeamRepository(AppDbContext db) => _db = db;

    public async Task<Team?> GetByIdAsync(Guid id)
    {
        Log.Debug("GetByIdAsync {Id}", id);
        return await _db.Teams
            .Include(t => t.Division)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Team>> GetAllAsync()
    {
        Log.Debug("GetAllAsync");
        return await _db.Teams
            .AsNoTracking()
            .Include(t => t.Division)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Team>> GetByDivisionAsync(Guid divisionId)
    {
        Log.Debug("GetByDivisionAsync {DivisionId}", divisionId);
        return await _db.Teams
            .AsNoTracking()
            .Include(t => t.Division)
            .Where(t => t.DivisionId == divisionId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task AddAsync(Team team)
    {
        Log.Info("AddAsync {Name}", team.Name);
        await _db.Teams.AddAsync(team);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Team team)
    {
        Log.Info("UpdateAsync {Id}", team.Id);
        _db.Teams.Update(team);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        Log.Info("DeleteAsync {Id}", id);
        var team = await _db.Teams.FindAsync(id);
        if (team is null) return;
        _db.Teams.Remove(team);
        await _db.SaveChangesAsync();
    }
}
