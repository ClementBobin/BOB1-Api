using Bob1.Shared.Contracts.Club;
using Bob1.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;

namespace Bob1.Modules.Identity;

public sealed class IdentityModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(o => o.UseSqlite(
            configuration.GetConnectionString("Default"),
            sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Identity")));

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<TokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<MembershipService>();
        services.AddScoped<IIdentityApi, IdentityApi>();
        services.AddHostedService<MembershipRolloverService>();
    }

    public async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();

        var forceSeed = services.GetRequiredService<IConfiguration>().GetValue<bool>("Database:Seed");
if ((!forceSeed && !services.GetRequiredService<IHostEnvironment>().IsDevelopment()) || (await db.Users.AnyAsync() && !forceSeed))
            return;

        // Development seed: three accounts you can log in with.
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var now = DateTime.UtcNow;

        var family = Family.Create("Martin", now);
        db.Families.Add(family);

        User Make(string email, string first, string last, UserRoles roles, string password, Guid? familyId)
        {
            var u = User.Create(email, first, last, roles, familyId, now);
            u.SetPasswordHash(hasher.HashPassword(u, password));
            return u;
        }

        db.Users.AddRange(
            Make("parent@bob1.local", "Camille", "Martin", UserRoles.Parent, "Parent123!", family.Id),
            Make("leo@bob1.local", "Léo", "Martin", UserRoles.Parent, "Leo12345!", family.Id),
            Make("staff@bob1.local", "Sam", "Arbitre", UserRoles.Staff, "Staff123!", null),
            Make("admin@bob1.local", "Alex", "Admin", UserRoles.Admin | UserRoles.Staff, "Admin123!", null));

        // Léo plays in U13 M this season (the season's memberships are the only place a division is stored).
        var leo = db.Users.Local.Single(u => u.Email == "leo@bob1.local");
        db.Memberships.Add(Membership.Create(leo.Id, Season.For(now), ClubSeed.DivisionU13M, now));
        db.TeamMemberships.Add(TeamMembership.Create(leo.Id, Season.For(now), ClubSeed.TeamU13MBob1));
        await db.SaveChangesAsync();
    }
}
