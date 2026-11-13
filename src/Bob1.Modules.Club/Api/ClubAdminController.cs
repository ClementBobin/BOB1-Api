using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Ranking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bob1.Modules.Club.Api;

internal sealed record DivisionPlayers(Guid DivisionId, string DivisionName, int Players);
internal sealed record SeasonOverviewDto(string Label, DateOnly StartsOn, DateOnly EndsOn, bool IsCurrent, int Players,
    IReadOnlyList<DivisionPlayers> PlayersByDivision, int MatchesTotal, int MatchesPlayed, int MatchesCancelled,
    int PointEntries, int PointsAwarded, int PointsRemoved, int ManualAdjustments);

[Route(ApiRoutes.V1 + "/admin")]
[Authorize(Policy = Policies.Admin)]
[Tags("Club (admin)")]
internal sealed class ClubAdminController(
    ClubDbContext db, TimeProvider clock, Bob1.Shared.Contracts.Notifications.INotificationsApi notices,
    Bob1.Shared.Contracts.Identity.IIdentityApi people, ICurrentUser actor, ILogger<ClubAdminController> log) : ApiControllerBase
{
    /// <summary>In-app alert for the other admins (no push): structural changes worth knowing about.</summary>
    private Task AlertAdminsAsync(string title, string body, string type, CancellationToken ct) =>
        Safe.RunAsync(log, title, () => notices.NotifyAdminsAsync(title, body,
            new Dictionary<string, string> { ["type"] = type }, actor.IsAuthenticated ? actor.UserId : null, ct));

    // ---- Seasons (read-only history) ----

    /// <summary>One row per season: players per division, matches and points, for the "Saisons" overview.</summary>
    [HttpQuery("seasons/overview")]
    public async Task<IActionResult> SeasonsOverview([FromServices] IIdentityApi identity, [FromServices] IMatchesApi matches,
        [FromServices] IRankingApi ranking, CancellationToken ct)
    {
        var current = Season.Current(clock);
        var seasons = await db.Seasons.AsNoTracking().OrderByDescending(s => s.Label).ToListAsync(ct);
        var divisionNames = await db.Divisions.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var items = new List<SeasonOverviewDto>();
        foreach (var s in seasons)
        {
            var players = await identity.GetSeasonPlayerCountsAsync(s.Label, ct);
            var m = await matches.GetSeasonStatsAsync(s.Label, ct);
            var p = await ranking.GetSeasonTotalsAsync(s.Label, ct);
            items.Add(new SeasonOverviewDto(s.Label, s.StartsOn, s.EndsOn, s.Label == current,
                players.Values.Sum(),
                players.Select(x => new DivisionPlayers(x.Key, divisionNames.GetValueOrDefault(x.Key, "?"), x.Value))
                    .OrderBy(x => x.DivisionName).ToList(),
                m.Total, m.Played, m.Cancelled, p.Entries, p.Awarded, p.Removed, p.ManualEntries));
        }
        return Ok(items);
    }

    // ---- Divisions ----

    [HttpPost("divisions")]
    public async Task<IActionResult> CreateDivision(DivisionRequest r, CancellationToken ct)
    {
        if (Guard.NotBlank(r.Name, "name", 50) is { } err) return err.ToProblem();
        if (Guard.NotBlank(r.Category, "category", 20) is { } err2) return err2.ToProblem();
        if (Guard.NotBlank(r.Gender, "gender", 20) is { } err3) return err3.ToProblem();
        var name = r.Name.Trim();
        if (await db.Divisions.AnyAsync(d => d.Name == name, ct))
            return Error.Conflict("division.name_taken", "A division with this name already exists.").ToProblem();
        var d = new Division { Name = name, Category = r.Category.Trim(), Gender = r.Gender.Trim(), IsActive = r.IsActive };
        db.Divisions.Add(d);
        await db.SaveChangesAsync(ct);
        return Created($"/api/v1/divisions/{d.Id}", new DivisionDto(d.Id, d.Name, d.Category, d.Gender, d.IsActive));
    }

    [HttpPut("divisions/{id:guid}")]
    public async Task<IActionResult> UpdateDivision(Guid id, DivisionRequest r, CancellationToken ct)
    {
        if (Guard.NotBlank(r.Name, "name", 50) is { } err) return err.ToProblem();
        if (Guard.NotBlank(r.Category, "category", 20) is { } err2) return err2.ToProblem();
        if (Guard.NotBlank(r.Gender, "gender", 20) is { } err3) return err3.ToProblem();
        var d = await db.Divisions.FindAsync([id], ct);
        if (d is null) return Error.NotFound("division.not_found", "Division not found.").ToProblem();
        d.Name = r.Name.Trim();
        d.Category = r.Category.Trim();
        d.Gender = r.Gender.Trim();
        var wasActive = d.IsActive;
        d.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        if (wasActive != d.IsActive)
            await AlertAdminsAsync(d.IsActive ? "Division réactivée" : "Division désactivée",
                $"La division {d.Name} a été {(d.IsActive ? "réactivée" : "désactivée")} : vérifiez ses joueurs et ses matchs à venir.",
                "club_entity_changed", ct);
        return Ok(new DivisionDto(d.Id, d.Name, d.Category, d.Gender, d.IsActive));
    }

    [HttpDelete("divisions/{id:guid}")]
    public async Task<IActionResult> DeleteDivision(Guid id, CancellationToken ct)
    {
        // NOTE: other modules reference divisions by id only (no cross-schema FK).
        // TODO: refuse deletion when users or matches still use it (ask IMatchesApi / IIdentityApi).
        var d = await db.Divisions.FindAsync([id], ct);
        if (d is null) return Error.NotFound("division.not_found", "Division not found.").ToProblem();
        if (await db.Teams.AnyAsync(t => t.DivisionId == id, ct))
            return Error.Conflict("division.has_teams", "Delete or move the teams of this division first.").ToProblem();
        db.Divisions.Remove(d);
        await db.SaveChangesAsync(ct);
        await AlertAdminsAsync("Division supprimée", $"La division {d.Name} a été supprimée.", "club_entity_changed", ct);
        return NoContent();
    }

    // ---- Teams ----

    [HttpPost("teams")]
    public async Task<IActionResult> CreateTeam(TeamRequest r, CancellationToken ct)
    {
        if (Guard.NotBlank(r.Name, "name", 100) is { } err) return err.ToProblem();
        if (!await db.Divisions.AnyAsync(d => d.Id == r.DivisionId, ct))
            return Error.NotFound("division.not_found", "Division not found.").ToProblem();
        var t = new Team { Name = r.Name.Trim(), DivisionId = r.DivisionId, CoachName = r.CoachName?.Trim(), CoachUserId = r.CoachUserId, LogoUrl = r.LogoUrl?.Trim(), IsActive = r.IsActive, TrainingVenueId = r.TrainingVenueId };
        db.Teams.Add(t);
        await db.SaveChangesAsync(ct);
        return Created($"/api/v1/teams/{t.Id}", new TeamDto(t.Id, t.Name, t.DivisionId, t.CoachName, t.CoachUserId, t.LogoUrl, t.IsActive, t.TrainingVenueId));
    }

    [HttpPut("teams/{id:guid}")]
    public async Task<IActionResult> UpdateTeam(Guid id, TeamRequest r, CancellationToken ct)
    {
        if (Guard.NotBlank(r.Name, "name", 100) is { } err) return err.ToProblem();
        var t = await db.Teams.FindAsync([id], ct);
        if (t is null) return Error.NotFound("team.not_found", "Team not found.").ToProblem();
        if (!await db.Divisions.AnyAsync(d => d.Id == r.DivisionId, ct))
            return Error.NotFound("division.not_found", "Division not found.").ToProblem();
        var (oldCoach, oldVenue, wasActive) = (t.CoachUserId, t.TrainingVenueId, t.IsActive);
        t.Name = r.Name.Trim();
        t.DivisionId = r.DivisionId;
        t.CoachName = r.CoachName?.Trim();
        t.CoachUserId = r.CoachUserId;
        t.LogoUrl = r.LogoUrl?.Trim();
        t.IsActive = r.IsActive;
        t.TrainingVenueId = r.TrainingVenueId;
        await db.SaveChangesAsync(ct);

        await Safe.RunAsync(log, "team changed", async () =>
        {
            var data = new Dictionary<string, string> { ["type"] = "team_changed", ["teamId"] = t.Id.ToString() };
            if (oldCoach != t.CoachUserId)
            {
                // Previous and new coach are both told; admins get an audit copy.
                if (oldCoach is { } previous)
                    await notices.SendToPersonAsync(previous, "Équipe : coach modifié", $"Vous n'êtes plus coach de {t.Name}.", data, ct);
                if (t.CoachUserId is { } next)
                    await notices.SendToPersonAsync(next, "Équipe : coach modifié", $"Vous êtes maintenant coach de {t.Name}.", data, ct);
                await notices.NotifyAdminsAsync("Coach modifié", $"Le coach de {t.Name} a changé.", data, actor.IsAuthenticated ? actor.UserId : null, ct);
            }
            if (oldVenue != t.TrainingVenueId)
            {
                var venueName = t.TrainingVenueId is { } v ? (await db.Venues.FindAsync([v], ct))?.Name ?? "?" : "aucun";
                var audience = (await people.GetUserIdsByTeamsAsync([t.Id], ct)).ToHashSet();
                if (t.CoachUserId is { } coachId) audience.Add(coachId);
                await notices.SendToUsersAsync(audience, "Lieu d'entraînement modifié", $"{t.Name} s'entraîne maintenant à : {venueName}.", data, ct);
            }
            if (wasActive != t.IsActive)
                await notices.NotifyAdminsAsync(t.IsActive ? "Équipe réactivée" : "Équipe désactivée",
                    $"L'équipe {t.Name} a été {(t.IsActive ? "réactivée" : "désactivée")} : vérifiez ses joueurs et ses matchs à venir.",
                    data, actor.IsAuthenticated ? actor.UserId : null, ct);
        });
        return Ok(new TeamDto(t.Id, t.Name, t.DivisionId, t.CoachName, t.CoachUserId, t.LogoUrl, t.IsActive, t.TrainingVenueId));
    }

    [HttpDelete("teams/{id:guid}")]
    public async Task<IActionResult> DeleteTeam(Guid id, CancellationToken ct)
    {
        var t = await db.Teams.FindAsync([id], ct);
        if (t is null) return Error.NotFound("team.not_found", "Team not found.").ToProblem();
        db.Teams.Remove(t);
        await db.SaveChangesAsync(ct);
        await AlertAdminsAsync("Équipe supprimée", $"L'équipe {t.Name} a été supprimée.", "club_entity_changed", ct);
        return NoContent();
    }

    // ---- Venues ----

    [HttpPost("venues")]
    public async Task<IActionResult> CreateVenue(VenueRequest r, CancellationToken ct)
    {
        if (Guard.NotBlank(r.Name, "name", 150) is { } err) return err.ToProblem();
        var v = new Venue { Name = r.Name.Trim(), Address = r.Address?.Trim(), City = r.City?.Trim(), PostalCode = r.PostalCode?.Trim() };
        db.Venues.Add(v);
        await db.SaveChangesAsync(ct);
        return Created($"/api/v1/venues/{v.Id}", new VenueDto(v.Id, v.Name, v.Address, v.City, v.PostalCode));
    }

    [HttpPut("venues/{id:guid}")]
    public async Task<IActionResult> UpdateVenue(Guid id, VenueRequest r, CancellationToken ct)
    {
        if (Guard.NotBlank(r.Name, "name", 150) is { } err) return err.ToProblem();
        var v = await db.Venues.FindAsync([id], ct);
        if (v is null) return Error.NotFound("venue.not_found", "Venue not found.").ToProblem();
        v.Name = r.Name.Trim();
        v.Address = r.Address?.Trim();
        v.City = r.City?.Trim();
        v.PostalCode = r.PostalCode?.Trim();
        await db.SaveChangesAsync(ct);
        return Ok(new VenueDto(v.Id, v.Name, v.Address, v.City, v.PostalCode));
    }

    [HttpDelete("venues/{id:guid}")]
    public async Task<IActionResult> DeleteVenue(Guid id, CancellationToken ct)
    {
        // TODO: refuse deletion when matches still reference it.
        var v = await db.Venues.FindAsync([id], ct);
        if (v is null) return Error.NotFound("venue.not_found", "Venue not found.").ToProblem();
        db.Venues.Remove(v);
        await db.SaveChangesAsync(ct);
        await AlertAdminsAsync("Lieu supprimé", $"Le lieu {v.Name} a été supprimé.", "club_entity_changed", ct);
        return NoContent();
    }
}
