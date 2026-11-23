namespace Bob1.Shared;

public static class Safe
{
    /// <summary>
    /// Runs a notification side effect after the data is saved: a failure is logged and never turns
    /// the admin's (already successful) action into an error.
    /// </summary>
    public static async Task RunAsync(Microsoft.Extensions.Logging.ILogger log, string what, Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { Microsoft.Extensions.Logging.LoggerExtensions.LogError(log, ex, "Notification handler failed: {What}", what); }
    }
}
