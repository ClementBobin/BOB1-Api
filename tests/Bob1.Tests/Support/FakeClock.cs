namespace Bob1.Tests;

/// <summary>Deterministic <see cref="TimeProvider"/>: time only moves when a test says so.</summary>
internal sealed class FakeClock(DateTime utcNow) : TimeProvider
{
    private DateTime _now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

    /// <summary>Same default everywhere: 13 Nov 2026, 17:30 UTC (season 2026-2027, 18h30 in Paris).</summary>
    public FakeClock() : this(new DateTime(2026, 11, 13, 17, 30, 0, DateTimeKind.Utc)) { }

    public DateTime Now => _now;

    public override DateTimeOffset GetUtcNow() => new(_now, TimeSpan.Zero);

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
