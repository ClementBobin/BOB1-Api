using Bob1.Shared.Contracts.Club;

namespace Bob1.Shared.Contracts.Matches;

public static class MatchDiff
{
    /// <summary>"date 13/11 à 18h30 → 14/11 à 10h00 ; lieu A → B ; équipes ..." for notification bodies.</summary>
    public static async Task<string> DescribeAsync(IClubApi club, MatchRescheduled e, CancellationToken ct)
    {
        var parts = new List<string>();
        if (e.OldStartsAtUtc != e.NewStartsAtUtc)
            parts.Add($"date {Dates.Label(e.OldStartsAtUtc)} → {Dates.Label(e.NewStartsAtUtc)}");
        if (e.OldVenueId != e.NewVenueId)
        {
            var venues = await club.GetVenueNamesAsync([e.OldVenueId, e.NewVenueId], ct);
            parts.Add($"lieu {venues.GetValueOrDefault(e.OldVenueId, "?")} → {venues.GetValueOrDefault(e.NewVenueId, "?")}");
        }
        if (e.OldHomeTeamId != e.NewHomeTeamId || e.OldAwayTeamId != e.NewAwayTeamId)
        {
            var teams = await club.GetTeamNamesAsync([e.OldHomeTeamId, e.OldAwayTeamId, e.NewHomeTeamId, e.NewAwayTeamId], ct);
            string T(Guid id) => teams.GetValueOrDefault(id, "?");
            parts.Add($"équipes {T(e.OldHomeTeamId)} vs {T(e.OldAwayTeamId)} → {T(e.NewHomeTeamId)} vs {T(e.NewAwayTeamId)}");
        }
        return string.Join(" ; ", parts);
    }
}
