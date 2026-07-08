namespace Infrastructure.Repositories;

using Domain.Entities;

using Infrastructure.Data;
using Infrastructure.Interfaces;

using Microsoft.EntityFrameworkCore;
using NLog;

public class MatchRepository : IMatchRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public MatchRepository(AppDbContext db) => _db = db;

    private IQueryable<Match> FullQuery() =>
        _db.Matches
            .Include(m => m.Division)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Location)
            .Include(m => m.Slots).ThenInclude(s => s.AssignedUser);

    public async Task<Match?> GetByIdAsync(Guid id)
    {
        _log.Debug("GetByIdAsync {Id}", id);
        return await FullQuery().FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<Match>> GetAllAsync()
    {
        _log.Debug("GetAllAsync");
        return await FullQuery()
            .AsNoTracking()
            .OrderBy(m => m.DateUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<Match>> GetByMonthAsync(int year, int month)
    {
        _log.Debug("GetByMonthAsync {Year}-{Month}", year, month);
        return await FullQuery()
            .AsNoTracking()
            .Where(m => m.DateUtc.Year == year && m.DateUtc.Month == month)
            .OrderBy(m => m.DateUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<Match>> GetByDivisionAsync(Guid divisionId)
    {
        _log.Debug("GetByDivisionAsync {DivisionId}", divisionId);
        return await FullQuery()
            .AsNoTracking()
            .Where(m => m.DivisionId == divisionId)
            .OrderBy(m => m.DateUtc)
            .ToListAsync();
    }

    public async Task AddAsync(Match match)
    {
        _log.Info("AddAsync match on {Date}", match.DateUtc);
        await _db.Matches.AddAsync(match);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Match match)
    {
        _log.Info("UpdateAsync {Id}", match.Id);
        _db.Matches.Update(match);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        _log.Info("DeleteAsync {Id}", id);
        var match = await _db.Matches.FindAsync(id);
        if (match is null) return;
        _db.Matches.Remove(match);
        await _db.SaveChangesAsync();
    }
}
