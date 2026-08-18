namespace Api.Middleware;

using Microsoft.AspNetCore.Mvc;
using NLog;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public GlobalExceptionMiddleware(RequestDelegate next)
        => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            await HandleAsync(ctx, ex);
        }
    }

    private static async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        var (status, message) = ex switch
        {
            KeyNotFoundException =>
                (StatusCodes.Status404NotFound, ex.Message),

            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, ex.Message),

            ArgumentException =>
                (StatusCodes.Status400BadRequest, ex.Message),

            _ =>
                (StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred.")
        };

        if (status >= 500)
            _log.Error(ex, "Unhandled exception");
        else
            _log.Warn(ex, "Handled exception → HTTP {Status}", status);

        if (ctx.Response.HasStarted)
        {
            _log.Error(ex, "Response already started; cannot write error response.");
            return;
        }

        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = status,
            Title = status >= 500
                ? "Internal Server Error"
                : "Request failed",
            Detail = message,
            Instance = ctx.Request.Path,
        };

        problem.Extensions["traceId"] = ctx.TraceIdentifier;

        await ctx.Response.WriteAsJsonAsync(problem);
    }
}