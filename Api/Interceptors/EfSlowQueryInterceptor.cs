namespace Api.Interceptors;

using System.Data.Common;
using System.Diagnostics;

using Api.Configuration;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

/// <summary>
/// EF Core interceptor that logs SQL commands exceeding the configured slow-query threshold.
/// </summary>
public class EfSlowQueryInterceptor : DbCommandInterceptor
{
    private readonly ILogger<EfSlowQueryInterceptor> _logger;
    private readonly int _thresholdMs;

    public EfSlowQueryInterceptor(ILogger<EfSlowQueryInterceptor> logger,
        IOptions<EfPerformanceOptions> options)
    {
        _logger = logger;
        _thresholdMs = options.Value.SeuilMs;
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    { LogIfSlow(eventData.Duration); return result; }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    { LogIfSlow(eventData.Duration); return ValueTask.FromResult(result); }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    { LogIfSlow(eventData.Duration); return result; }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    { LogIfSlow(eventData.Duration); return ValueTask.FromResult(result); }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    { LogIfSlow(eventData.Duration); return result; }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    { LogIfSlow(eventData.Duration); return ValueTask.FromResult(result); }

    private static string FindRepositoryCaller()
    {
        var frames = new StackTrace(skipFrames: 1, fNeedFileInfo: false).GetFrames();
        foreach (var frame in frames)
        {
            var method = frame.GetMethod();
            if (method?.DeclaringType?.Namespace?.StartsWith("Infrastructure.Repositories", StringComparison.Ordinal) == true)
                return $"{method.DeclaringType.Name}.{method.Name}";
        }
        return "unknown";
    }

    private void LogIfSlow(TimeSpan duration)
    {
        if (duration.TotalMilliseconds > _thresholdMs)
            _logger.LogWarning(
                "[SlowQuery] {DurationMs:F1} ms (threshold {ThresholdMs} ms) — caller: {Caller}",
                duration.TotalMilliseconds, _thresholdMs, FindRepositoryCaller());
    }
}
