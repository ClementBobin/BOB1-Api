using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

using Domain.Entities;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<PointRule> PointRules => Set<PointRule>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<RoleSlot> RoleSlots => Set<RoleSlot>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Penalty> Penalties => Set<Penalty>();
    public DbSet<AppNotification> Notifications => Set<AppNotification>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── User ──────────────────────────────────────────────────────────
        mb.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasConversion<string>();
        });

        // ── Division ──────────────────────────────────────────────────────
        mb.Entity<Division>(e =>
        {
            e.HasIndex(d => d.Name).IsUnique();
        });

        // ── Team ──────────────────────────────────────────────────────────
        mb.Entity<Team>(e =>
        {
            e.HasOne(t => t.Division)
             .WithMany(d => d.Teams)
             .HasForeignKey(t => t.DivisionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── PointRule ─────────────────────────────────────────────────────
        mb.Entity<PointRule>(e =>
        {
            e.HasIndex(p => p.Role).IsUnique();
            e.Property(p => p.Role).HasConversion<string>();
        });

        // ── Match ─────────────────────────────────────────────────────────
        mb.Entity<Match>(e =>
        {
            e.HasOne(m => m.Division)
             .WithMany(d => d.Matches)
             .HasForeignKey(m => m.DivisionId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.HomeTeam)
             .WithMany()
             .HasForeignKey(m => m.HomeTeamId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.AwayTeam)
             .WithMany()
             .HasForeignKey(m => m.AwayTeamId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.Location)
             .WithMany(l => l.Matches)
             .HasForeignKey(m => m.LocationId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── RoleSlot ──────────────────────────────────────────────────────
        mb.Entity<RoleSlot>(e =>
        {
            e.HasOne(rs => rs.AssignedUser)
             .WithMany()
             .HasForeignKey(rs => rs.AssignedUserId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne<Match>()
             .WithMany(m => m.Slots)
             .HasForeignKey(rs => rs.MatchId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(rs => rs.Role).HasConversion<string>();

            // One role per match
            e.HasIndex(rs => new { rs.MatchId, rs.Role }).IsUnique();
        });

        // ── Subscription ──────────────────────────────────────────────────
        mb.Entity<Subscription>(e =>
        {
            e.HasOne(s => s.User)
             .WithMany(u => u.Subscriptions)
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.Match)
             .WithMany(m => m.Subscriptions)
             .HasForeignKey(s => s.MatchId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(s => s.Role).HasConversion<string>();
            e.Property(s => s.Status).HasConversion<string>();

            // One active subscription per user per match
            e.HasIndex(s => new { s.UserId, s.MatchId }).IsUnique();
        });

        // ── Penalty ───────────────────────────────────────────────────────
        mb.Entity<Penalty>(e =>
        {
            e.HasOne(p => p.User)
             .WithMany(u => u.Penalties)
             .HasForeignKey(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.Match)
             .WithMany()
             .HasForeignKey(p => p.MatchId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── AppNotification ───────────────────────────────────────────────
        mb.Entity<AppNotification>(e =>
        {
            e.HasOne(n => n.User)
             .WithMany(u => u.Notifications)
             .HasForeignKey(n => n.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(n => n.Match)
             .WithMany(m => m.Notifications)
             .HasForeignKey(n => n.MatchId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);

            e.Property(n => n.Type).HasConversion<string>();

            e.HasIndex(n => new { n.UserId, n.IsRead });
        });
    }
}
