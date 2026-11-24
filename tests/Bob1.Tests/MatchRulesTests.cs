using Bob1.Modules.Participation.Application;
using Bob1.Shared.Contracts.Matches;

namespace Bob1.Tests;

public class MatchRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 17, 30, 0, DateTimeKind.Utc);

    private static MatchInfo Match(DateTime startsAtUtc, MatchStatus status = MatchStatus.Scheduled) =>
        new(Guid.NewGuid(), "Bob1 vs Rivals", "Gymnase", startsAtUtc, status, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void A_missing_match_is_reported() =>
        Assert.Same(ParticipationErrors.MatchNotFound, MatchRules.EnsureOpen(null, Now));

    [Fact]
    public void A_future_scheduled_match_is_open() =>
        Assert.Null(MatchRules.EnsureOpen(Match(Now.AddMinutes(1)), Now));

    [Fact]
    public void A_cancelled_match_is_closed() =>
        Assert.Same(ParticipationErrors.MatchCancelled, MatchRules.EnsureOpen(Match(Now.AddDays(3), MatchStatus.Cancelled), Now));

    [Fact]
    public void A_started_match_is_closed() =>
        Assert.Same(ParticipationErrors.MatchAlreadyStarted, MatchRules.EnsureOpen(Match(Now.AddMinutes(-1)), Now));

    [Fact]
    public void A_match_starting_right_now_is_already_started() =>
        Assert.Same(ParticipationErrors.MatchAlreadyStarted, MatchRules.EnsureOpen(Match(Now), Now));

    [Fact]
    public void Cancelled_wins_over_started() =>
        Assert.Same(ParticipationErrors.MatchCancelled, MatchRules.EnsureOpen(Match(Now.AddDays(-1), MatchStatus.Cancelled), Now));

    [Fact]
    public void Error_codes_are_stable()
    {
        Assert.Equal("match.not_found", ParticipationErrors.MatchNotFound.Code);
        Assert.Equal("match.cancelled", ParticipationErrors.MatchCancelled.Code);
        Assert.Equal("match.already_started", ParticipationErrors.MatchAlreadyStarted.Code);
        Assert.Equal("participation.not_in_team", ParticipationErrors.NotInTeam.Code);
        Assert.Equal(ErrorType.Forbidden, ParticipationErrors.NotInTeam.Type);
    }
}
