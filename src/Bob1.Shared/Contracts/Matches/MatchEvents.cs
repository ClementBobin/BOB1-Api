namespace Bob1.Shared.Contracts.Matches;

/// <summary>A match was scheduled (published after it is saved).</summary>
public sealed record MatchCreated(
    Guid MatchId, string Title, Guid DivisionId, Guid HomeTeamId, Guid AwayTeamId, Guid VenueId, DateTime StartsAtUtc) : DomainEvent;

/// <summary>Date, venue or teams of a scheduled match changed. Carries old and new values for the "old -> new" diff.</summary>
public sealed record MatchRescheduled(
    Guid MatchId, string Title,
    DateTime OldStartsAtUtc, DateTime NewStartsAtUtc,
    Guid OldVenueId, Guid NewVenueId,
    Guid OldHomeTeamId, Guid OldAwayTeamId, Guid NewHomeTeamId, Guid NewAwayTeamId) : DomainEvent;
