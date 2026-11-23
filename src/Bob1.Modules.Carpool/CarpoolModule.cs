using Bob1.Shared.Contracts.Carpool;

namespace Bob1.Modules.Carpool;

public sealed class CarpoolModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CarpoolDbContext>(o => o.UseNpgsql(
            configuration.GetConnectionString("Default"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Carpool")));
        services.AddScoped<ICarpoolApi, CarpoolApi>();
        services.AddHostedService<RideReminderService>();
        services.AddScoped<IDomainEventHandler<Bob1.Shared.Contracts.Identity.UserDeactivated>, DriverDeactivatedHandler>();
        services.AddScoped<IDomainEventHandler<Bob1.Shared.Contracts.Matches.MatchCancelled>, MatchCancelledRidesHandler>();
        services.AddScoped<IDomainEventHandler<Bob1.Shared.Contracts.Matches.MatchRescheduled>, MatchRescheduledRidesHandler>();
    }

    public async Task InitializeAsync(IServiceProvider services) =>
        await services.GetRequiredService<CarpoolDbContext>().Database.MigrateAsync();
}
