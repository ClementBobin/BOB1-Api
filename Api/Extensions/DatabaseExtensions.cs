namespace Api.Extensions;

using Configuration;
using Interceptors;

using Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services,
        IConfiguration config, IWebHostEnvironment env)
    {
        services.Configure<EfPerformanceOptions>(config.GetSection("EfPerformance"));
        services.AddSingleton<EfSlowQueryInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetRequiredService<EfSlowQueryInterceptor>());

            var provider = config["DatabaseProvider"]
                ?? (env.IsProduction() ? "postgres" : "sqlite");

            if (provider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                options.UseNpgsql(config.GetConnectionString("DefaultConnection"));
            else
                options.UseSqlite(config.GetConnectionString("DefaultConnection"));

            options.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        return services;
    }
}
