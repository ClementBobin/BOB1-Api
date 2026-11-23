using Bob1.Shared.Contracts.Matches;

namespace Bob1.Modules.Matches.Domain;

/// <summary>A match between two teams (ids only: names live in the Club module).</summary>
internal sealed class Match : Entity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Season { get; private set; } = default!;   // derived from the start date ("2026-2027")
    public Guid HomeTeamId { get; private set; }
    public Guid AwayTeamId { get; private set; }
    public Guid VenueId { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public MatchStatus Status { get; private set; } = MatchStatus.Scheduled;

    private Match() { }

    public static Match Create(Guid homeTeamId, Guid awayTeamId, Guid venueId,
        DateTime startsAtUtc, Guid? id = null)
    {
        var m = new Match { Id = id ?? Guid.NewGuid() };
        m.Apply(homeTeamId, awayTeamId, venueId, startsAtUtc);
        return m;
    }

    public Result Update(Guid homeTeamId, Guid awayTeamId, Guid venueId, DateTime startsAtUtc)
    {
        if (Status != MatchStatus.Scheduled)
            return Error.Conflict("match.not_editable", "Only scheduled matches can be edited.");
        Apply(homeTeamId, awayTeamId, venueId, startsAtUtc);
        return Result.Success();
    }

    /// <param name="title">Display title ("Home vs Away"), resolved by the caller from the team names.</param>
    public Result Cancel(string title)
    {
        if (Status != MatchStatus.Scheduled)
            return Error.Conflict("match.not_cancellable", "Only scheduled matches can be cancelled.");
        Status = MatchStatus.Cancelled;
        Raise(new MatchCancelled(Id, title, StartsAtUtc, HomeTeamId, AwayTeamId));
        return Result.Success();
    }

    public Result MarkPlayed()
    {
        if (Status == MatchStatus.Cancelled)
            return Error.Conflict("match.cancelled", "A cancelled match cannot be played.");
        Status = MatchStatus.Played;
        return Result.Success();
    }

    private void Apply(Guid homeTeamId, Guid awayTeamId, Guid venueId, DateTime startsAtUtc)
    {
        HomeTeamId = homeTeamId; AwayTeamId = awayTeamId;
        VenueId = venueId; StartsAtUtc = startsAtUtc;
        Season = Bob1.Shared.Season.For(startsAtUtc);
    }
}
