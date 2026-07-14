using Api.Extensions;
using Api.Middleware;

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
            else
                policy.WithOrigins(
                        builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                        ?? ["https://bob1.local"])
                      .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        });
    });

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Bob1 API",
            Version = "v1",
        });
        options.CustomSchemaIds(type => type.FullName);
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "JWT Authorization — example: \"Bearer <token>\"",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
        });
        options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", doc)] = [],
        });
    });

    // Extensions
    builder.Services.AddDatabase(builder.Configuration, builder.Environment);
    builder.Services.AddJwtAuth(builder.Configuration);
    builder.Services.AddAppServices(builder.Configuration);
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Migrations + seed (dev: always seed; prod: migrate only)
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (app.Environment.IsDevelopment() || args.Contains("--seed"))
            await DbInitializer.SeedAsync(context);
        else
            await context.Database.MigrateAsync();
    }

    // Docs (non-prod only)
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

    // Middleware pipeline (order matters)
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    });
    app.UseMiddleware<GlobalExceptionMiddleware>(); // global error handler
    app.UseHttpsRedirection();
    app.UseCors("Frontend");
    app.UseCookiePolicy();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
    logger.Fatal(ex, "Application failed to start");
    throw;
}

public partial class Program;
