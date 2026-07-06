using Api.Extensions;

using Infrastructure.Data;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

using NLog;
using NLog.Web;

using Scalar.AspNetCore;

try
{
    var builder = WebApplication.CreateBuilder(args);

    var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
    logger.Debug("init main");

    builder.Services.Configure<CookiePolicyOptions>(options =>
    {
        options.CheckConsentNeeded = _ => false;
        options.MinimumSameSitePolicy = SameSiteMode.None;
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Frontend", policy =>
        {
            if (builder.Environment.IsDevelopment())
                policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
                      .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            else if (builder.Environment.IsStaging())
                policy.WithOrigins("https://staging.projet-cyna.fr")
                      .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            else
                policy.WithOrigins("https://projet-cyna.fr", "https://www.projet-cyna.fr")
                      .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        });
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "CynaApi API", Version = "v1" });
        options.CustomSchemaIds(type => type.FullName);
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "JWT Authorization header using the Bearer scheme. Exemple : \"Bearer 12345abcdef\"",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    });

    // Extensions
    builder.Services.AddDatabase(builder.Configuration, builder.Environment);
    builder.Services.AddJwtAuth(builder.Configuration);
    builder.Services.AddAppServices(builder.Configuration);
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Migrations + seed
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();

        if (args.Contains("--seed") && !app.Environment.IsProduction())
            await DbInitializer.SeedAsync(context);
    }

    // Docs (hors prod)
    var apiDocs = builder.Configuration["ApiDocs"] ?? "Scalar";
    if (!app.Environment.IsProduction())
    {
        app.MapOpenApi();

        if (apiDocs.Equals("Swagger", StringComparison.OrdinalIgnoreCase))
        {
            app.UseSwagger();
            app.UseSwaggerUI(o =>
            {
                o.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                o.RoutePrefix = string.Empty;
            });
        }
        else
        {
            app.MapScalarApiReference();
        }

        app.MapGet("/", () => Results.Redirect(
            apiDocs.Equals("Swagger", StringComparison.OrdinalIgnoreCase) ? "/swagger" : "/scalar/v1"
        ));
    }

    app.MapHealthChecks("/health");

    // Pipeline middleware (ordre important)
    app.UseForwardedHeaders(new ForwardedHeadersOptions   // 0. Proxy (X-Forwarded-Proto → scheme HTTPS)
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    });
    app.UseHttpsRedirection();   // 1. HTTPS
    app.UseCors("Frontend");     // 2. CORS
    app.UseCookiePolicy();       // 3. Cookies
    app.UseAuthentication();     // 4. Auth
    app.UseAuthorization();      // 5. Autorisation
    app.MapControllers();        // 6. Routes

    app.Run();
}
catch (Exception ex)
{
    var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
    logger.Fatal(ex, "Application failed to start");
    throw;
}

public partial class Program;