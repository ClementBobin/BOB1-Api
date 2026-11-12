using System.Data.Common;

namespace Bob1.Shared;

public static class DbErrors
{
    /// <summary>PostgreSQL SQLSTATE 23505: unique_violation (unique index or primary key).</summary>
    public static bool IsUniqueViolation(this Exception ex) =>
        ex.InnerException is DbException { SqlState: "23505" };
}
