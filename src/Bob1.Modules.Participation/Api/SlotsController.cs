using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bob1.Modules.Participation.Api;

internal sealed record SlotRequest(string Kind, string Role, int Capacity, DateTime? StartsAtUtc, DateTime? EndsAtUtc);
internal sealed record UrgentSlotDto(Guid SlotId, Guid MatchId, string MatchTitle, DateTime MatchStartsAtUtc, string Kind, string Role, int Missing);

/// <summary>Join / leave (parents for Volunteer slots, staff for Staff slots; checked in the handler).</summary>
internal sealed record UrgentQuery(int? Days, int? Skip, int? Take);
/// <summary>Page of open slots plus the grand total of missing places (for dashboard KPIs).</summary>
internal sealed record UrgentSlotPage(int Total, int TotalMissing, IReadOnlyList<UrgentSlotDto> Items);

[Route(ApiRoutes.V1 + "/slots/{slotId:guid}/join")]
[Authorize]
[Tags("Participation")]
internal sealed class SlotsController(SlotMembershipHandler h) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Join(Guid slotId, CancellationToken ct)
    {
        var r = await h.JoinAsync(slotId, ct);
        return r.IsSuccess ? NoContent() : r.Error.ToProblem();
    }

    [HttpDelete]
    public async Task<IActionResult> Leave(Guid slotId, CancellationToken ct)
    {
        var r = await h.LeaveAsync(slotId, ct);
        return r.IsSuccess ? NoContent() : r.Error.ToProblem();
    }
}

[Route(ApiRoutes.V1 + "/admin")]
[Authorize(Policy = Policies.Admin)]
[Tags("Participation (admin)")]
internal sealed class SlotsAdminController(
    ParticipationDbContext db, IMatchesApi matches, IIdentityApi identity, INotificationsApi notifications,
    ICurrentUser actor, TimeProvider clock, ILogger<SlotsAdminController> log) : ApiControllerBase
{
    [HttpPost("matches/{matchId:guid}/slots")]
    public async Task<IActionResult> Create(Guid matchId, SlotRequest r, CancellationToken ct)
    {
        if (Validate(r, out var kind) is { } err) return err.ToProblem();
        var match = await matches.GetAsync(matchId, ct);
        if (match is null) return ParticipationErrors.MatchNotFound.ToProblem();

        var slot = Slot.Create(matchId, kind, r.Role.Trim(), r.Capacity, Dates.ToUtc(r.StartsAtUtc), Dates.ToUtc(r.EndsAtUtc));
        db.Slots.Add(slot);
        await db.SaveChangesAsync(ct);

        // A new open slot: staff slots go to the staff, volunteer slots to the parents of the match's teams.
        await Safe.RunAsync(log, "slot created", async () =>
        {
            IEnumerable<Guid> audience = kind == SlotKind.Staff
                ? await identity.GetUserIdsByRoleAsync("Staff", ct)
                : (await identity.GetUserIdsByTeamsAsync([match.HomeTeamId, match.AwayTeamId], ct))
                    .Intersect(await identity.GetUserIdsByRoleAsync("Parent", ct));
            await notifications.SendToUsersAsync(audience, "Nouveau poste à pourvoir",
                $"{slot.Role} ({slot.Capacity} place(s)) sur {match.Title}, {Dates.Label(match.StartsAtUtc)}.",
                new Dictionary<string, string> { ["type"] = "slot_created", ["matchId"] = matchId.ToString(), ["slotId"] = slot.Id.ToString() }, ct);
        });
        return Created($"/api/v1/matches/{matchId}/participation", new { id = slot.Id });
    }

    [HttpPut("slots/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SlotRequest r, CancellationToken ct)
    {
        if (Validate(r, out _) is { } err) return err.ToProblem();
        var slot = await db.Slots.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (slot is null) return ParticipationErrors.SlotNotFound.ToProblem();

        var (oldRole, oldCapacity, oldStart, oldEnd) = (slot.Role, slot.Capacity, slot.StartsAtUtc, slot.EndsAtUtc);
        var updated = slot.Update(r.Role.Trim(), r.Capacity, Dates.ToUtc(r.StartsAtUtc), Dates.ToUtc(r.EndsAtUtc));
        if (!updated.IsSuccess) return updated.Error.ToProblem();
        await db.SaveChangesAsync(ct);

        // Tell the people already signed up what changed (role, hours); a lower capacity does not eject anyone (it is refused below Filled).
        if (oldRole != slot.Role || oldStart != slot.StartsAtUtc || oldEnd != slot.EndsAtUtc || oldCapacity != slot.Capacity)
            await Safe.RunAsync(log, "slot updated", async () =>
            {
                var signed = await db.SlotSignups.Where(x => x.SlotId == id).Select(x => x.UserId).ToListAsync(ct);
                var changes = new List<string>();
                if (oldRole != slot.Role) changes.Add($"rôle {oldRole} → {slot.Role}");
                if (oldStart != slot.StartsAtUtc || oldEnd != slot.EndsAtUtc) changes.Add("horaires modifiés");
                if (oldCapacity != slot.Capacity) changes.Add($"places {oldCapacity} → {slot.Capacity}");
                var title = (await matches.GetAsync(slot.MatchId, ct))?.Title ?? "le match";
                await notifications.SendToUsersAsync(signed, "Poste modifié", $"{title} : {string.Join(", ", changes)}.",
                    new Dictionary<string, string> { ["type"] = "slot_updated", ["matchId"] = slot.MatchId.ToString(), ["slotId"] = id.ToString() }, ct);
            });
        return NoContent();
    }

    [HttpDelete("slots/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var slot = await db.Slots.Include(s => s.Signups).SingleOrDefaultAsync(s => s.Id == id, ct);
        if (slot is null) return ParticipationErrors.SlotNotFound.ToProblem();

        // People signed up are warned (signups cascade; no penalty: the admin removed the slot, nobody withdrew).
        var signed = slot.Signups.Select(x => x.UserId).ToList();
        var (matchId, role) = (slot.MatchId, slot.Role);
        db.Slots.Remove(slot);
        await db.SaveChangesAsync(ct);

        if (signed.Count > 0)
            await Safe.RunAsync(log, "slot deleted", async () =>
            {
                var title = (await matches.GetAsync(matchId, ct))?.Title ?? "le match";
                await notifications.SendToUsersAsync(signed, "Poste supprimé",
                    $"Le poste {role} sur {title} a été supprimé : votre inscription est annulée.",
                    new Dictionary<string, string> { ["type"] = "slot_deleted", ["matchId"] = matchId.ToString() }, ct);
            });
        return NoContent();
    }

    // The back-office "urgent" list: slots still open on matches starting soon.
    [HttpQuery("slots/urgent")]
    public async Task<IActionResult> Urgent([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UrgentQuery? query, CancellationToken ct)
    {
        query ??= new(null, null, null);
        var now = clock.GetUtcNow().UtcDateTime;
        var upcoming = (await matches.GetStartingBetweenAsync(now, now.AddDays(Math.Clamp(query.Days ?? 7, 1, 60)), ct))
            .ToDictionary(m => m.Id);
        var ids = upcoming.Keys.ToList();

        var open = await db.Slots.AsNoTracking().Where(s => ids.Contains(s.MatchId) && s.Filled < s.Capacity).ToListAsync(ct);
        var all = open
            .Select(s => new UrgentSlotDto(s.Id, s.MatchId, upcoming[s.MatchId].Title, upcoming[s.MatchId].StartsAtUtc,
                s.Kind.ToString(), s.Role, s.Capacity - s.Filled))
            .OrderBy(x => x.MatchStartsAtUtc).ThenBy(x => x.Role).ToList();
        var items = all.Skip(Paging.Skip(query.Skip)).Take(Paging.Take(query.Take, 100, 500)).ToList();
        return Ok(new UrgentSlotPage(all.Count, all.Sum(x => x.Missing), items));
    }

    private static Error? Validate(SlotRequest r, out SlotKind kind)
    {
        kind = SlotKind.Volunteer;
        if (!Enum.TryParse(r.Kind, ignoreCase: true, out kind))
            return Error.Validation("kind.invalid", "Kind must be 'Volunteer' or 'Staff'.");
        return Guard.First(Guard.NotBlank(r.Role, "role", 50), Guard.InRange(r.Capacity, "capacity", 1, 50));
    }
}
