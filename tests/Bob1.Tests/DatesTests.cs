namespace Bob1.Tests;

public class DatesTests
{
    [Fact]
    public void ToUtc_keeps_utc_values()
    {
        var utc = new DateTime(2026, 11, 13, 17, 30, 0, DateTimeKind.Utc);

        var result = Dates.ToUtc(utc);

        Assert.Equal(utc, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void ToUtc_treats_unspecified_as_utc_without_shifting_the_time()
    {
        var unspecified = new DateTime(2026, 11, 13, 17, 30, 0, DateTimeKind.Unspecified);

        var result = Dates.ToUtc(unspecified);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(unspecified.Ticks, result.Ticks);
    }

    [Fact]
    public void ToUtc_converts_local_values()
    {
        var local = new DateTime(2026, 11, 13, 17, 30, 0, DateTimeKind.Local);

        var result = Dates.ToUtc(local);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(local.ToUniversalTime(), result);
    }

    [Fact]
    public void ToUtc_handles_nullable_values()
    {
        Assert.Null(Dates.ToUtc((DateTime?)null));
        Assert.Equal(DateTimeKind.Utc, Dates.ToUtc((DateTime?)new DateTime(2026, 1, 1))!.Value.Kind);
    }

    // Label() formats in Europe/Paris (needs the system time zone database, present on Linux/macOS/Windows runners).
    [Theory]
    [InlineData(2026, 11, 13, 17, 30, "13/11 à 18h30")]   // winter time, UTC+1
    [InlineData(2026, 7, 13, 16, 30, "13/07 à 18h30")]    // summer time, UTC+2
    public void Label_uses_the_club_local_time(int year, int month, int day, int hour, int minute, string expected) =>
        Assert.Equal(expected, Dates.Label(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc)));
}
