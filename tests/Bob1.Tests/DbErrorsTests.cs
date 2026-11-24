using System.Data.Common;

namespace Bob1.Tests;

public class DbErrorsTests
{
    private sealed class FakeDbException(string? sqlState) : DbException
    {
        public override string? SqlState => sqlState;
    }

    [Fact]
    public void Detects_a_postgres_unique_violation() =>
        Assert.True(new Exception("save failed", new FakeDbException("23505")).IsUniqueViolation());

    [Fact]
    public void Ignores_other_database_errors() =>
        Assert.False(new Exception("save failed", new FakeDbException("23503")).IsUniqueViolation());

    [Fact]
    public void Ignores_exceptions_without_a_database_cause()
    {
        Assert.False(new Exception("plain").IsUniqueViolation());
        Assert.False(new Exception("wrapped", new InvalidOperationException()).IsUniqueViolation());
    }
}
