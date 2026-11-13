using Bob1.Shared.Contracts.Club;
using Bob1.Shared.Contracts.Matches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bob1.Modules.Matches.Api;


[Route(ApiRoutes.V1 + "/admin/matches")]
[Authorize(Policy = Policies.Admin)]
[Tags("Matches (admin)")]
internal sealed class MatchesAdminController(MatchesDbContext db, IClubApi club, IDomainEventDispatcher events, TimeProvider clock)
    : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(MatchRequest r, CancellationToken ct)
    {
        if (await ValidateAsync(r, ct) is { } invalid) return invalid.ToProblem();

        var m = Match.Create(r.DivisionId, r.HomeTeamId, r.AwayTeamId, r.VenueId, Dates.ToUtc(r.StartsAtUtc));
        db.Matches.Add(m);
        await db.SaveChangesAsync(ct);

        var labels = await MatchLabels.LoadAsync(club, [m], ct);
        await events.DispatchAsync([new MatchCreated(m.Id, labels.Title(m), m.DivisionId, m.HomeTeamId, m.AwayTeamId, m.VenueId, m.StartsAtUtc)], ct);
        return Created($"/api/v1/matches/{m.Id}", m.ToDto(labels));
    }

    [HttpPut("{matchId:guid}")]
    public async Task<IActionResult> Update(Guid matchId, MatchRequest r, CancellationToken ct)
    {
        var m = await db.Matches.SingleOrDefaultAsync(x => x.Id == matchId, ct);
        if (m is null) return Error.NotFound("match.not_found", "Match not found.").ToProblem();
        if (IsClosed(m)) return ClosedSeason().ToProblem();

        if (await ValidateAsync(r, ct) is { } invalid) return invalid.ToProblem();

        var (oldStart, oldVenue, oldHome, oldAway) = (m.StartsAtUtc, m.VenueId, m.HomeTeamId, m.AwayTeamId);
        var updated = m.Update(r.DivisionId, r.HomeTeamId, r.AwayTeamId, r.VenueId, Dates.ToUtc(r.StartsAtUtc));
        if (!updated.IsSuccess) return updated.Error.ToProblem();
        await db.SaveChangesAsync(ct);

        var labels = await MatchLabels.LoadAsync(club, [m], ct);
        if (oldStart != m.StartsAtUtc || oldVenue != m.VenueId || oldHome != m.HomeTeamId || oldAway != m.AwayTeamId)
            await events.DispatchAsync([new MatchRescheduled(m.Id, labels.Title(m), oldStart, m.StartsAtUtc,
                oldVenue, m.VenueId, oldHome, oldAway, m.HomeTeamId, m.AwayTeamId)], ct);
        return Ok(m.ToDto(labels));
    }

    [HttpPost("{matchId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid matchId, CancellationToken ct)
    {
        var m = await db.Matches.SingleOrDefaultAsync(x => x.Id == matchId, ct);
        if (m is null) return Error.NotFound("match.not_found", "Match not found.").ToProblem();
        if (IsClosed(m)) return ClosedSeason().ToProblem();

        var labels = await MatchLabels.LoadAsync(club, [m], ct);
        var cancelled = m.Cancel(labels.Title(m));
        if (!cancelled.IsSuccess) return cancelled.Error.ToProblem();
        await db.SaveChangesAsync(ct);

        await events.DispatchAsync(m.DomainEvents, ct);   // Participation notifies the families
        m.ClearDomainEvents();
        return Ok(m.ToDto(labels));
    }

    [HttpPost("{matchId:guid}/played")]
    public async Task<IActionResult> MarkPlayed(Guid matchId, CancellationToken ct)
    {
        var m = await db.Matches.SingleOrDefaultAsync(x => x.Id == matchId, ct);
        if (m is null) return Error.NotFound("match.not_found", "Match not found.").ToProblem();
        if (IsClosed(m)) return ClosedSeason().ToProblem();

        var played = m.MarkPlayed();
        if (!played.IsSuccess) return played.Error.ToProblem();
        await db.SaveChangesAsync(ct);
        return Ok(await m.ToDtoAsync(club, ct));
    }

    private async Task<Error?> ValidateAsync(MatchRequest r, CancellationToken ct)
    {
        if (r.HomeTeamId == r.AwayTeamId)
            return Error.Validation("teams.same", "The home and away teams must be different.");
        var startsAt = Dates.ToUtc(r.StartsAtUtc);
        if (startsAt <= clock.GetUtcNow().UtcDateTime.AddDays(-1))
            return Error.Validation("starts_at.invalid", "The match date cannot be in the past.");
        if (Season.For(startsAt) != Season.Current(clock))
            return Error.Validation("starts_at.out_of_season",
                $"The match must fall inside the current season ({Season.StartsOn(Season.Current(clock)):dd/MM/yyyy} – {Season.EndsOn(Season.Current(clock)):dd/MM/yyyy}).");

        if (await club.GetDivisionAsync(r.DivisionId, ct) is null)
            return Error.NotFound("division.not_found", "Division not found.");
        var teams = await club.GetTeamNamesAsync([r.HomeTeamId, r.AwayTeamId], ct);
        if (!teams.ContainsKey(r.HomeTeamId)) return Error.NotFound("team.not_found", "Home team not found.");
        if (!teams.ContainsKey(r.AwayTeamId)) return Error.NotFound("team.not_found", "Away team not found.");
        if (await club.GetVenueAsync(r.VenueId, ct) is null)
            return Error.NotFound("venue.not_found", "Venue not found.");
        return null;
    }

    /// <summary>Past seasons are history: read-only.</summary>
    private bool IsClosed(Match m) => m.Season != Season.Current(clock);

    private static Error ClosedSeason() =>
        Error.Conflict("season.closed", "This match belongs to a past season and can no longer be changed.");
}
