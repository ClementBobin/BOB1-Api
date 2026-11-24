using Bob1.Modules.Ranking.Application;
using Bob1.Modules.Ranking.Domain;

namespace Bob1.Tests;

public class PointEntryTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 17, 30, 0, DateTimeKind.Utc);

    private static PointEntry Entry(string sourceKey) =>
        PointEntry.Create(Guid.NewGuid(), "presence", 10, "Présence confirmée", sourceKey, "2026-2027", null, null, Now);

    [Fact]
    public void Only_entries_created_by_an_admin_are_manual()
    {
        Assert.True(Entry($"{PointEntry.ManualPrefix}{Guid.NewGuid()}").IsManual);
        Assert.False(Entry("presence:match:person").IsManual);
        Assert.False(Entry($"{PointEntry.RevokePrefix}presence:match:person").IsManual);
    }

    [Fact]
    public void The_manual_prefix_is_case_sensitive() => Assert.False(Entry("MANUAL:abc").IsManual);

    [Fact]
    public void UpdateManual_changes_the_editable_fields_only()
    {
        var entry = PointEntry.Create(Guid.NewGuid(), "presence", -20, "Absence", "manual:1", "2026-2027", null, Guid.NewGuid(), Now);
        var matchId = Guid.NewGuid();
        var person = entry.PersonId;

        entry.UpdateManual("volunteer", -5, "Désistement", matchId);

        Assert.Equal("volunteer", entry.Category);
        Assert.Equal(-5, entry.Points);
        Assert.Equal("Désistement", entry.Reason);
        Assert.Equal(matchId, entry.MatchId);
        Assert.Equal("manual:1", entry.SourceKey);   // the idempotency key never changes
        Assert.Equal(person, entry.PersonId);
        Assert.Equal("2026-2027", entry.Season);
    }

    [Fact]
    public void Default_rules_have_unique_keys_and_penalties_are_negative()
    {
        Assert.Equal(RuleKeys.Defaults.Length, RuleKeys.Defaults.Select(r => r.Key).Distinct().Count());
        Assert.All(RuleKeys.Defaults.Where(r => r.Key.StartsWith("penalty.", StringComparison.Ordinal)), r => Assert.True(r.Points < 0));
        Assert.All(RuleKeys.Defaults.Where(r => !r.Key.StartsWith("penalty.", StringComparison.Ordinal)), r => Assert.True(r.Points > 0));
    }
}
