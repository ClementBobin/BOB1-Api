namespace Api.Middleware;

using System.Text.Json;

using NLog;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public GlobalExceptionMiddleware(RequestDelegate next) => _next = next;

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
            KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, ex.Message),
            InvalidOperationException => (StatusCodes.Status400BadRequest, ex.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

        if (status == StatusCodes.Status500InternalServerError)
            Log.Error(ex, "Unhandled exception");
        else
            Log.Warn(ex, "Handled exception → HTTP {Status}", status);

        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode = status;

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status,
            message,
            traceId = ctx.TraceIdentifier,
        }));
    }
}
