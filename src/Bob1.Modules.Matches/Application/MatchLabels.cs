using Bob1.Shared.Contracts.Club;

namespace Bob1.Modules.Matches.Application;

/// <summary>Display names (teams, venue) of a set of matches, resolved once through the Club module.</summary>
internal sealed record MatchLabels(
    IReadOnlyDictionary<Guid, string> Teams,
    IReadOnlyDictionary<Guid, string> Venues)
{
    public string Title(Match m) => $"{Team(m.HomeTeamId)} vs {Team(m.AwayTeamId)}";
    public string Venue(Match m) => Venues.GetValueOrDefault(m.VenueId, "?");

    private string Team(Guid id) => Teams.GetValueOrDefault(id, "?");

    public static async Task<MatchLabels> LoadAsync(IClubApi club, IReadOnlyCollection<Match> matches, CancellationToken ct) => new(
        await club.GetTeamNamesAsync(matches.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId }), ct),
        await club.GetVenueNamesAsync(matches.Select(m => m.VenueId), ct));
}
