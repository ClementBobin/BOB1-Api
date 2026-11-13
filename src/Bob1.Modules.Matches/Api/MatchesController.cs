using Bob1.Shared.Contracts.Club;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bob1.Modules.Matches.Api;

internal sealed record MatchDto(
    Guid Id, string Title, Guid HomeTeamId, Guid AwayTeamId,
    Guid VenueId, DateTime StartsAtUtc, MatchStatus Status, string Season);

internal sealed record MatchRequest(Guid HomeTeamId, Guid AwayTeamId, Guid VenueId, DateTime StartsAtUtc);

internal static class MatchMapping
{
    public static MatchDto ToDto(this Match m, MatchLabels l) =>
        new(m.Id, l.Title(m), m.HomeTeamId, m.AwayTeamId, m.VenueId, m.StartsAtUtc, m.Status, m.Season);

    public static async Task<MatchDto> ToDtoAsync(this Match m, IClubApi club, CancellationToken ct) =>
        m.ToDto(await MatchLabels.LoadAsync(club, [m], ct));
}

internal sealed record MatchQuery(
    Guid? MemberId, DateTime? From, DateTime? To, string? Status, string? Search, string? Season, int? Skip, int? Take);

[Route(ApiRoutes.V1 + "/matches")]
[Authorize]
[Tags("Matches")]
internal sealed class MatchesController(MatchesDbContext db, IClubApi club, IIdentityApi identity, ICurrentUser user, TimeProvider clock)
    : ApiControllerBase
{
    // Calendar. Filters: family member (the matches of the teams he/she plays in), date range. Without a season it defaults to the next 120 days; with one it lists the whole season.
    [HttpQuery]
    public async Task<IActionResult> List([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MatchQuery? query, CancellationToken ct)
    {
        query ??= new(null, null, null, null, null, null, null, null);
        var (memberId, from, to) = (query.MemberId, query.From, query.To);
        List<Guid>? memberTeams = null;
        if (memberId is { } mid)
        {
            var member = await identity.GetMemberAsync(mid, ct);
            if (member is null || member.FamilyId != user.FamilyId)
                return Error.NotFound("member.not_found", "Family member not found.").ToProblem();
            memberTeams = (await identity.GetTeamIdsOfUserAsync(mid, ct)).ToList();
        }

        var q = db.Matches.AsNoTracking();
        if (Season.IsValid(query.Season))
        {
            // A whole season (history); from / to only narrow it further.
            var label = query.Season!;
            q = q.Where(m => m.Season == label);
            if (Dates.ToUtc(from) is { } f) q = q.Where(m => m.StartsAtUtc >= f);
            if (Dates.ToUtc(to) is { } t) q = q.Where(m => m.StartsAtUtc <= t);
        }
        else
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var fromUtc = Dates.ToUtc(from) ?? now.Date;
            var toUtc = Dates.ToUtc(to) ?? fromUtc.AddDays(120);
            q = q.Where(m => m.StartsAtUtc >= fromUtc && m.StartsAtUtc <= toUtc);
        }

        if (memberTeams is not null)
            q = q.Where(m => memberTeams.Contains(m.HomeTeamId) || memberTeams.Contains(m.AwayTeamId));

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<MatchStatus>(query.Status, true, out var status))
            q = q.Where(m => m.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Names live in the Club module: resolve the ids that match, then filter the matches on them.
            var hits = await club.SearchAsync(query.Search, ct);
            var (teams, venues) = (hits.TeamIds, hits.VenueIds);
            q = q.Where(m => venues.Contains(m.VenueId)
                             || teams.Contains(m.HomeTeamId) || teams.Contains(m.AwayTeamId));
        }

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(m => m.StartsAtUtc)
            .Skip(Paging.Skip(query.Skip)).Take(Paging.Take(query.Take, 50, 300)).ToListAsync(ct);
        var labels = await MatchLabels.LoadAsync(club, rows, ct);
        return Ok(new PagedResult<MatchDto>(total, rows.Select(m => m.ToDto(labels)).ToList()));
    }

    [HttpGet("{matchId:guid}")]
    public async Task<IActionResult> Get(Guid matchId, CancellationToken ct)
    {
        var m = await db.Matches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == matchId, ct);
        return m is null ? Error.NotFound("match.not_found", "Match not found.").ToProblem() : Ok(await m.ToDtoAsync(club, ct));
    }
}