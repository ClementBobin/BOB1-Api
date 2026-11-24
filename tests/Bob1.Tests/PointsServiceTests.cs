using Bob1.Modules.Ranking.Application;
using Bob1.Modules.Ranking.Domain;
using Bob1.Modules.Ranking.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Bob1.Tests;

public class PointsServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeClock _clock = new();
    private readonly RankingDbContext _db = TestDb.Ranking();
    private readonly PointsService _sut;
    private readonly Guid _person = Guid.NewGuid();

    public PointsServiceTests()
    {
        // Copies, so no test shares (or mutates) the static default instances.
        _db.Rules.AddRange(RuleKeys.Defaults.Select(r => new RankingRule { Key = r.Key, Category = r.Category, Label = r.Label, Points = r.Points }));
        _db.SaveChanges();
        _sut = new PointsService(_db, _clock);
    }

    private Task<int> ScoreAsync() => _db.PointEntries.Where(p => p.PersonId == _person).SumAsync(p => p.Points, Ct);

    // ---- Award --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Award_writes_the_points_of_the_rule()
    {
        var matchId = Guid.NewGuid();

        await _sut.AwardAsync(RuleKeys.Presence, _person, "presence:1", reason: null, matchId, Ct);

        var entry = await _db.PointEntries.SingleAsync(Ct);
        Assert.Equal(_person, entry.PersonId);
        Assert.Equal("presence", entry.Category);
        Assert.Equal(10, entry.Points);
        Assert.Equal("Présence confirmée", entry.Reason);   // the rule's label when no reason is given
        Assert.Equal("presence:1", entry.SourceKey);
        Assert.Equal("2026-2027", entry.Season);
        Assert.Equal(matchId, entry.MatchId);
        Assert.Null(entry.ActorUserId);
        Assert.Equal(_clock.Now, entry.CreatedAtUtc);
    }

    [Fact]
    public async Task Award_prefers_an_explicit_reason()
    {
        await _sut.AwardAsync(RuleKeys.VolunteerSlot, _person, "slot:1", "Buvette du samedi", null, Ct);

        Assert.Equal("Buvette du samedi", (await _db.PointEntries.SingleAsync(Ct)).Reason);
    }

    [Fact]
    public async Task Award_is_idempotent_per_source_key()
    {
        await _sut.AwardAsync(RuleKeys.Presence, _person, "presence:1", null, null, Ct);
        await _sut.AwardAsync(RuleKeys.Presence, _person, "presence:1", null, null, Ct);

        Assert.Equal(1, await _db.PointEntries.CountAsync(Ct));
        Assert.Equal(10, await ScoreAsync());
    }

    [Fact]
    public async Task Different_source_keys_add_up()
    {
        await _sut.AwardAsync(RuleKeys.Presence, _person, "presence:1", null, null, Ct);
        await _sut.AwardAsync(RuleKeys.StaffSlot, _person, "slot:2", null, null, Ct);
        await _sut.AwardAsync(RuleKeys.CarpoolOffer, _person, "ride:3", null, null, Ct);

        Assert.Equal(10 + 20 + 10, await ScoreAsync());
    }

    [Fact]
    public async Task Award_ignores_unknown_rules()
    {
        await _sut.AwardAsync("does.not.exist", _person, "x:1", null, null, Ct);

        Assert.Empty(_db.PointEntries);
    }

    [Fact]
    public async Task Award_ignores_a_rule_switched_off_with_zero_points()
    {
        var rule = await _db.Rules.SingleAsync(r => r.Key == RuleKeys.Presence, Ct);
        rule.Points = 0;
        await _db.SaveChangesAsync(Ct);

        await _sut.AwardAsync(RuleKeys.Presence, _person, "presence:1", null, null, Ct);

        Assert.Empty(_db.PointEntries);
    }

    [Fact]
    public async Task A_penalty_rule_subtracts_points()
    {
        await _sut.AwardAsync(RuleKeys.Presence, _person, "presence:1", null, null, Ct);
        await _sut.AwardAsync(RuleKeys.PenaltyNoShow, _person, "noshow:1", null, null, Ct);

        Assert.Equal(10 - 20, await ScoreAsync());
    }

    // ---- Revoke -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Revoke_appends_the_opposite_entry_and_keeps_the_original()
    {
        var matchId = Guid.NewGuid();
        await _sut.AwardAsync(RuleKeys.VolunteerSlot, _person, "slot:1", null, matchId, Ct);

        var revoked = await _sut.RevokeAsync("slot:1", "revoke:slot:1", Ct);

        Assert.True(revoked);
        Assert.Equal(2, await _db.PointEntries.CountAsync(Ct));
        Assert.Equal(0, await ScoreAsync());
        var reversal = await _db.PointEntries.SingleAsync(p => p.SourceKey == "revoke:slot:1", Ct);
        Assert.Equal(-15, reversal.Points);
        Assert.Equal("volunteer", reversal.Category);
        Assert.Equal(matchId, reversal.MatchId);
        Assert.Equal("Annulation : Bénévolat (buvette...)", reversal.Reason);
        Assert.False(reversal.IsManual);
        Assert.Equal(15, (await _db.PointEntries.SingleAsync(p => p.SourceKey == "slot:1", Ct)).Points);   // original untouched
    }

    [Fact]
    public async Task Revoke_twice_does_not_cancel_the_points_twice()
    {
        await _sut.AwardAsync(RuleKeys.Presence, _person, "presence:1", null, null, Ct);

        await _sut.RevokeAsync("presence:1", "revoke:presence:1", Ct);
        var again = await _sut.RevokeAsync("presence:1", "revoke:presence:1", Ct);

        Assert.True(again);
        Assert.Equal(2, await _db.PointEntries.CountAsync(Ct));
        Assert.Equal(0, await ScoreAsync());
    }

    [Fact]
    public async Task Revoke_of_an_unknown_entry_reports_false_and_writes_nothing()
    {
        var revoked = await _sut.RevokeAsync("missing:1", "revoke:missing:1", Ct);

        Assert.False(revoked);
        Assert.Empty(_db.PointEntries);
    }

    [Fact]
    public async Task The_reversal_stays_in_the_season_of_the_original_entry()
    {
        await _sut.AwardAsync(RuleKeys.Presence, _person, "presence:1", null, null, Ct);
        _clock.Advance(TimeSpan.FromDays(300));   // well into the next season

        await _sut.RevokeAsync("presence:1", "revoke:presence:1", Ct);

        var reversal = await _db.PointEntries.SingleAsync(p => p.SourceKey == "revoke:presence:1", Ct);
        Assert.Equal("2026-2027", reversal.Season);
        Assert.Equal(_clock.Now, reversal.CreatedAtUtc);
    }

    // ---- Manual entries -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AddManual_records_the_admin_and_a_manual_source_key()
    {
        var admin = Guid.NewGuid();
        var matchId = Guid.NewGuid();

        var entry = await _sut.AddManualAsync(_person, "presence", -5, "Retard", matchId, admin, Ct);

        Assert.True(entry.IsManual);
        Assert.StartsWith(PointEntry.ManualPrefix, entry.SourceKey);
        Assert.Equal(admin, entry.ActorUserId);
        Assert.Equal(-5, entry.Points);
        Assert.Equal("Retard", entry.Reason);
        Assert.Equal(matchId, entry.MatchId);
        Assert.Equal("2026-2027", entry.Season);
        Assert.Equal(1, await _db.PointEntries.CountAsync(Ct));
    }

    [Fact]
    public async Task Two_manual_entries_never_collide()
    {
        var admin = Guid.NewGuid();

        var first = await _sut.AddManualAsync(_person, "presence", -5, "Retard", null, admin, Ct);
        var second = await _sut.AddManualAsync(_person, "presence", -5, "Retard", null, admin, Ct);

        Assert.NotEqual(first.SourceKey, second.SourceKey);
        Assert.Equal(-10, await ScoreAsync());
    }
}
