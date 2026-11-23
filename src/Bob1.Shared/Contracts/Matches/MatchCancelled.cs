namespace Bob1.Shared.Contracts.Matches;

public sealed record MatchCancelled(Guid MatchId, string Title, DateTime StartsAtUtc, Guid HomeTeamId, Guid AwayTeamId) : DomainEvent;
