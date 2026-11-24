using Bob1.Modules.Matches.Domain;
using Bob1.Shared.Contracts.Matches;

namespace Bob1.Tests;

public class MatchTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid Away = Guid.NewGuid();
    private static readonly Guid Venue = Guid.NewGuid();
    private static readonly DateTime Start = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

    private static Match NewMatch() => Match.Create(Home, Away, Venue, Start);

    [Fact]
    public void Create_starts_scheduled_with_the_given_data()
    {
        var id = Guid.NewGuid();

        var match = Match.Create(Home, Away, Venue, Start, id);

        Assert.Equal(id, match.Id);
        Assert.Equal(Home, match.HomeTeamId);
        Assert.Equal(Away, match.AwayTeamId);
        Assert.Equal(Venue, match.VenueId);
        Assert.Equal(Start, match.StartsAtUtc);
        Assert.Equal(MatchStatus.Scheduled, match.Status);
        Assert.Empty(match.DomainEvents);
    }

    [Theory]
    [InlineData(2026, 8, 31, "2025-2026")]
    [InlineData(2026, 9, 1, "2026-2027")]
    [InlineData(2027, 6, 1, "2026-2027")]
    public void The_season_is_derived_from_the_start_date(int year, int month, int day, string expected) =>
        Assert.Equal(expected, Match.Create(Home, Away, Venue, new DateTime(year, month, day, 10, 0, 0, DateTimeKind.Utc)).Season);

    [Fact]
    public void Update_changes_a_scheduled_match_and_recomputes_the_season()
    {
        var match = NewMatch();
        var newHome = Guid.NewGuid();
        var newAway = Guid.NewGuid();
        var newVenue = Guid.NewGuid();
        var nextSeason = new DateTime(2027, 9, 5, 14, 0, 0, DateTimeKind.Utc);

        var result = match.Update(newHome, newAway, newVenue, nextSeason);

        Assert.True(result.IsSuccess);
        Assert.Equal(newHome, match.HomeTeamId);
        Assert.Equal(newAway, match.AwayTeamId);
        Assert.Equal(newVenue, match.VenueId);
        Assert.Equal(nextSeason, match.StartsAtUtc);
        Assert.Equal("2027-2028", match.Season);
    }

    [Fact]
    public void Update_is_refused_once_the_match_is_cancelled()
    {
        var match = NewMatch();
        match.Cancel("A vs B");

        var result = match.Update(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Start.AddDays(1));

        Assert.False(result.IsSuccess);
        Assert.Equal("match.not_editable", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(Home, match.HomeTeamId);   // untouched
        Assert.Equal(Start, match.StartsAtUtc);
    }

    [Fact]
    public void Update_is_refused_once_the_match_is_played()
    {
        var match = NewMatch();
        match.MarkPlayed();

        var result = match.Update(Home, Away, Venue, Start.AddDays(1));

        Assert.Equal("match.not_editable", result.Error.Code);
    }

    [Fact]
    public void Cancel_marks_the_match_and_raises_MatchCancelled()
    {
        var match = NewMatch();

        var result = match.Cancel("Bob1 vs Rivals");

        Assert.True(result.IsSuccess);
        Assert.Equal(MatchStatus.Cancelled, match.Status);
        var raised = Assert.IsType<MatchCancelled>(Assert.Single(match.DomainEvents));
        Assert.Equal(match.Id, raised.MatchId);
        Assert.Equal("Bob1 vs Rivals", raised.Title);
        Assert.Equal(Start, raised.StartsAtUtc);
        Assert.Equal(Home, raised.HomeTeamId);
        Assert.Equal(Away, raised.AwayTeamId);
    }

    [Fact]
    public void Cancel_twice_is_a_conflict_and_raises_nothing_more()
    {
        var match = NewMatch();
        match.Cancel("A vs B");

        var again = match.Cancel("A vs B");

        Assert.Equal("match.not_cancellable", again.Error.Code);
        Assert.Single(match.DomainEvents);
    }

    [Fact]
    public void A_played_match_cannot_be_cancelled()
    {
        var match = NewMatch();
        match.MarkPlayed();

        var result = match.Cancel("A vs B");

        Assert.Equal("match.not_cancellable", result.Error.Code);
        Assert.Equal(MatchStatus.Played, match.Status);
    }

    [Fact]
    public void MarkPlayed_works_on_a_scheduled_match()
    {
        var match = NewMatch();

        Assert.True(match.MarkPlayed().IsSuccess);
        Assert.Equal(MatchStatus.Played, match.Status);
    }

    [Fact]
    public void A_cancelled_match_cannot_be_played()
    {
        var match = NewMatch();
        match.Cancel("A vs B");

        var result = match.MarkPlayed();

        Assert.Equal("match.cancelled", result.Error.Code);
        Assert.Equal(MatchStatus.Cancelled, match.Status);
    }

    [Fact]
    public void ClearDomainEvents_empties_the_list()
    {
        var match = NewMatch();
        match.Cancel("A vs B");

        match.ClearDomainEvents();

        Assert.Empty(match.DomainEvents);
    }
}
