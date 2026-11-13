using Bob1.Shared.Contracts.Matches;

namespace Bob1.Modules.Matches.Infrastructure;

internal sealed class MatchesDbContext(DbContextOptions<MatchesDbContext> options) : DbContext(options)
{
    public DbSet<Match> Matches => Set<Match>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Match>(e =>
        {
            e.ToTable("matches");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Ignore(x => x.DomainEvents);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.StartsAtUtc);
            e.Property(x => x.Season).HasMaxLength(9);
            e.HasIndex(x => new { x.Season, x.StartsAtUtc });
            e.HasIndex(x => x.HomeTeamId);
            e.HasIndex(x => x.AwayTeamId);
        });
    }
}
