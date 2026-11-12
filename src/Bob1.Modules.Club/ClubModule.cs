using Bob1.Shared.Contracts.Club;

namespace Bob1.Modules.Club;

public sealed class ClubModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ClubDbContext>(o => o.UseSqlite(
            configuration.GetConnectionString("Default"),
            sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Club")));
        services.AddScoped<IClubApi, ClubApi>();
        services.AddHostedService<SeasonService>();
    }

    public async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ClubDbContext>();
        await db.Database.MigrateAsync();
        await SeasonEnsurer.EnsureCurrentAsync(db, services.GetRequiredService<TimeProvider>());

        var forceSeed = services.GetRequiredService<IConfiguration>().GetValue<bool>("Database:Seed");
        if ((!forceSeed && !services.GetRequiredService<IHostEnvironment>().IsDevelopment()) || (await db.Divisions.AnyAsync() && !forceSeed))
            return;

        db.Divisions.AddRange(
            new Division { Id = ClubSeed.DivisionU13M, Name = "U13 M", Category = "U13", Gender = "Masculin" },
            new Division { Id = ClubSeed.DivisionU17F, Name = "U17 F", Category = "U17", Gender = "Féminin" });
        db.Teams.AddRange(
            new Team { Id = ClubSeed.TeamU13MBob1, Name = "U13 M BOB1", DivisionId = ClubSeed.DivisionU13M, TrainingVenueId = ClubSeed.VenueCoubertin },
            new Team { Id = ClubSeed.TeamU13MCastelnau, Name = "Castelnau", DivisionId = ClubSeed.DivisionU13M });
        db.Venues.Add(new Venue { Id = ClubSeed.VenueCoubertin, Name = "Gymnase Coubertin", Address = "Montpellier" });
        await db.SaveChangesAsync();
    }
}
