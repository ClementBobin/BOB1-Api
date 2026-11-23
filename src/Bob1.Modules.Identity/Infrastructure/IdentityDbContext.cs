
namespace Bob1.Modules.Identity.Infrastructure;

internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FamilyInvitation> Invitations => Set<FamilyInvitation>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Ignore(x => x.DisplayName);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.FamilyId);
        });

        b.Entity<Family>(e =>
        {
            e.ToTable("families");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(150);
        });

        b.Entity<Membership>(e =>
        {
            e.ToTable("memberships");
            e.HasKey(x => new { x.UserId, x.Season });   // one division per user and season
            e.Property(x => x.Season).HasMaxLength(9);
            e.HasIndex(x => new { x.Season, x.DivisionId });
        });
        b.Entity<TeamMembership>(e =>
        {
            e.ToTable("team_memberships");
            e.HasKey(x => new { x.UserId, x.Season, x.TeamId });   // several teams per user and season
            e.Property(x => x.Season).HasMaxLength(9);
            e.HasIndex(x => new { x.Season, x.TeamId });
        });
        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        b.Entity<FamilyInvitation>(e =>
        {
            e.ToTable("family_invitations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Code).HasMaxLength(16);
            e.HasIndex(x => x.Code).IsUnique();
        });
    }
}
