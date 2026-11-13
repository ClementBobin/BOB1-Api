namespace Bob1.Shared.Contracts.Ranking;

/// <summary>What other modules may ask Ranking.</summary>
public interface IRankingApi
{
    Task<RankingSeasonTotals> GetSeasonTotalsAsync(string season, CancellationToken ct);
    /// <summary>Points lost for a late withdrawal: before J-15 (null = free), between J-15 and J-4, after J-4.</summary>
    Task<LateCancelRule> GetLateCancelRuleAsync(CancellationToken ct);
}

/// <summary>Awarded = sum of positive entries, Removed = sum of negative ones (as a negative number).</summary>
public sealed record RankingSeasonTotals(int Entries, int Awarded, int Removed, int ManualEntries);

public sealed record LateCancelRule(int Points, int? PointsJ15, int? PointsJ4);
