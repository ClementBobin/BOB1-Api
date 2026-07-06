using Infrastructure.Security;

namespace Api.Extensions;

using Application.Interfaces;
using Application.Services;

using Infrastructure.Interfaces;
using Infrastructure.Repositories;

public static class AppServicesExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services,
        IConfiguration config)
    {
        // --- Repositories ---
        services.AddScoped<ITokenGenerator, JwtTokenGenerator>();

        // --- Services ---

        return services;
    }
}