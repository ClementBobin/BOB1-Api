using Bob1.Modules.Identity.Infrastructure;
using Bob1.Modules.Ranking.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Bob1.Tests;

/// <summary>One isolated in-memory database per call (a new name each time), so tests never share data.</summary>
internal static class TestDb
{
    public static IdentityDbContext Identity() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public static RankingDbContext Ranking() =>
        new(new DbContextOptionsBuilder<RankingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
