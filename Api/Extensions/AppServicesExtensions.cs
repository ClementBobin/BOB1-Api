namespace Api.Extensions;

using Application.Interfaces;
using Application.Services;

using Infrastructure.Configuration;
using Infrastructure.Interfaces;
using Infrastructure.Repositories;
using Infrastructure.Security;

public static class AppServicesExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services,
        IConfiguration config)
    {
        // ── JWT options (used by JwtTokenGenerator in Infrastructure) ─────
        services.Configure<JwtOptions>(config.GetSection("Jwt"));

        // ── Repositories ──────────────────────────────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IDivisionRepository, DivisionRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IPenaltyRepository, PenaltyRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IPointRuleRepository, PointRuleRepository>();

        // ── Infrastructure services ───────────────────────────────────────
        services.AddScoped<ITokenGenerator, JwtTokenGenerator>();

        // ── Application services ──────────────────────────────────────────
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IDivisionService, DivisionService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IPenaltyService, PenaltyService>();
        services.AddScoped<IPointRuleService, PointRuleService>();

        return services;
    }
}
