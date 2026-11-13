using System.Security.Cryptography;
using Bob1.Shared.Contracts.Club;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bob1.Modules.Identity.Api;

internal sealed record PersonDto(Guid Id, string Email, string FirstName, string LastName, string[] Roles, bool IsActive, bool SuspendedFromParticipation, Guid? FamilyId, Guid? DivisionId, Guid[] TeamIds);
internal sealed record UpdateRolesRequest(string[] Roles);
internal sealed record SetActiveRequest(bool IsActive);
internal sealed record CreatePersonRequest(string Email, string FirstName, string LastName, string[] Roles, Guid? FamilyId, Guid? DivisionId, string? Password, Guid[]? TeamIds = null);
internal sealed record UpdatePersonRequest(string Email, string FirstName, string LastName, Guid? FamilyId, Guid? DivisionId, Guid[]? TeamIds = null);   // TeamIds null = leave the teams unchanged
internal sealed record CreatePersonResponse(PersonDto Person, string? TemporaryPassword);

/// <summary>Status: "active" (enabled, can participate), "suspended" (enabled but barred from participating) or "deactivated" (account disabled).</summary>
/// <summary>Season: whose division to show (default: current season).</summary>
internal sealed record PeopleQuery(string? Search, string? Role, bool? IsActive, string? Status, string? Season, int? Skip, int? Take);
internal sealed record MembershipDto(string Season, Guid DivisionId, string DivisionName, TeamRef[] Teams);
internal sealed record TeamRef(Guid Id, string Name);

[Route(ApiRoutes.V1 + "/admin/people")]
[Authorize(Policy = Policies.Admin)]
[Tags("People (admin)")]
internal sealed class PeopleAdminController(IdentityDbContext db, IClubApi club, MembershipService memberships, IPasswordHasher<Domain.User> hasher, ICurrentUser me, TimeProvider clock,
    PeopleNotices notices, IDomainEventDispatcher events, ILogger<PeopleAdminController> log)
    : ApiControllerBase
{
    [HttpQuery]
    public async Task<IActionResult> List([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PeopleQuery? query, CancellationToken ct)
    {
        query ??= new(null, null, null, null, null, null, null);
        var q = db.Users.AsNoTracking();
        if (Paging.Term(query.Search) is { } s)
            q = q.Where(u => u.Email.Contains(s) || u.FirstName.ToLower().Contains(s) || u.LastName.ToLower().Contains(s));
        if (!string.IsNullOrWhiteSpace(query.Role) && Enum.TryParse<UserRoles>(query.Role, true, out var r) && r != UserRoles.None)
            q = q.Where(u => (u.Roles & r) == r);
        if (query.IsActive is { } active) q = q.Where(u => u.IsActive == active);
        q = query.Status?.ToLowerInvariant() switch
        {
            "active" => q.Where(u => u.IsActive && !u.SuspendedFromParticipation),
            "suspended" => q.Where(u => u.IsActive && u.SuspendedFromParticipation),
            "deactivated" => q.Where(u => !u.IsActive),
            _ => q
        };

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Skip(Paging.Skip(query.Skip)).Take(Paging.Take(query.Take, 50, 200)).ToListAsync(ct);

        var season = Season.IsValid(query.Season) ? query.Season! : await memberships.EnsureCurrentSeasonAsync(ct);
        var divisions = await memberships.DivisionsAsync(season, rows.Select(u => u.Id), ct);
        var teams = await memberships.TeamsAsync(season, rows.Select(u => u.Id), ct);
        return Ok(new
        {
            total,
            items = rows.Select(u => ToDto(u, divisions.TryGetValue(u.Id, out var d) ? d : null,
                teams.TryGetValue(u.Id, out var t) ? t : null))
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePersonRequest r, CancellationToken ct)
    {
        var invalid = Guard.First(
            Guard.NotBlank(r.Email, "email"),
            Guard.NotBlank(r.FirstName, "first_name", 100),
            Guard.NotBlank(r.LastName, "last_name", 100));
        if (invalid is not null) return invalid.ToProblem();
        if (!r.Email.Contains('@')) return Error.Validation("email.invalid", "Invalid email address.").ToProblem();
        if (r.Roles is null || !UserRolesExtensions.TryParse(r.Roles, out var roles) || roles == UserRoles.None)
            return Error.Validation("roles.invalid", "Roles must be a non-empty list of Parent, Staff, Admin.").ToProblem();

        var email = r.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Error.Conflict("email.taken", "An account with this email already exists.").ToProblem();
        if (r.FamilyId is { } familyId && !await db.Families.AnyAsync(f => f.Id == familyId, ct))
            return Error.NotFound("family.not_found", "Family not found.").ToProblem();
        if (r.DivisionId is { } divisionId && await club.GetDivisionAsync(divisionId, ct) is null)
            return Error.NotFound("division.not_found", "Division not found.").ToProblem();
        if (r.TeamIds is { Length: > 0 } teamIds && (await club.GetTeamNamesAsync(teamIds, ct)).Count != teamIds.Distinct().Count())
            return Error.NotFound("team.not_found", "One of the teams was not found.").ToProblem();

        var temporaryPassword = string.IsNullOrWhiteSpace(r.Password) ? NewTemporaryPassword() : null;
        var password = temporaryPassword ?? r.Password!;
        if (password.Length < 8)
            return Error.Validation("password.too_short", "Password must be at least 8 characters.").ToProblem();

        // Fully qualified: inside a controller, "User" alone resolves to ControllerBase.User (the ClaimsPrincipal).
        var user = Domain.User.Create(email, r.FirstName.Trim(), r.LastName.Trim(), roles, r.FamilyId,
            clock.GetUtcNow().UtcDateTime);
        user.SetPasswordHash(hasher.HashPassword(user, password));
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return Error.Conflict("email.taken", "An account with this email already exists.").ToProblem();
        }

        await memberships.SetCurrentAsync(user.Id, r.DivisionId, ct);   // division of the current season only
        await memberships.SetCurrentTeamsAsync(user.Id, r.TeamIds ?? [], ct);
        await notices.UnassignedAsync(user.Id, ct);
        return Created($"/api/v1/admin/people/{user.Id}",
            new CreatePersonResponse(ToDto(user, r.DivisionId, r.TeamIds), temporaryPassword));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdatePersonRequest r, CancellationToken ct)
    {
        var invalid = Guard.First(
            Guard.NotBlank(r.Email, "email"),
            Guard.NotBlank(r.FirstName, "first_name", 100),
            Guard.NotBlank(r.LastName, "last_name", 100));
        if (invalid is not null) return invalid.ToProblem();
        if (!r.Email.Contains('@')) return Error.Validation("email.invalid", "Invalid email address.").ToProblem();

        var u = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return Error.NotFound("user.not_found", "User not found.").ToProblem();

        var email = r.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Id != id && x.Email == email, ct))
            return Error.Conflict("email.taken", "An account with this email already exists.").ToProblem();
        if (r.FamilyId is { } familyId && !await db.Families.AnyAsync(f => f.Id == familyId, ct))
            return Error.NotFound("family.not_found", "Family not found.").ToProblem();
        if (r.DivisionId is { } divisionId && await club.GetDivisionAsync(divisionId, ct) is null)
            return Error.NotFound("division.not_found", "Division not found.").ToProblem();
        if (r.TeamIds is { Length: > 0 } teamIds && (await club.GetTeamNamesAsync(teamIds, ct)).Count != teamIds.Distinct().Count())
            return Error.NotFound("team.not_found", "One of the teams was not found.").ToProblem();

        var oldFamily = u.FamilyId;
        var oldEmail = u.Email;
        var oldDivision = await CurrentDivisionAsync(u.Id, ct);
        var oldTeams = await CurrentTeamsAsync(u.Id, ct) ?? [];

        u.SetName(r.FirstName.Trim(), r.LastName.Trim());
        u.SetEmail(email);
        u.SetFamilyId(r.FamilyId);
        await db.SaveChangesAsync(ct);
        await memberships.SetCurrentAsync(u.Id, r.DivisionId, ct);   // only the current season changes: history is kept
        if (r.TeamIds is not null) await memberships.SetCurrentTeamsAsync(u.Id, r.TeamIds, ct);

        var newTeams = await CurrentTeamsAsync(u.Id, ct) ?? [];
        await notices.ClubChangedAsync(u.Id, oldDivision, r.DivisionId, oldTeams, newTeams, ct);
        await notices.UnassignedAsync(u.Id, ct);
        if (oldEmail != u.Email) await notices.EmailChangedAsync(u.Id, ct);
        await notices.FamilyChangedAsync(u.Id, oldFamily, r.FamilyId, ct);
        return Ok(ToDto(u, r.DivisionId, newTeams));
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> UpdateRoles(Guid id, UpdateRolesRequest r, CancellationToken ct)
    {
        if (r.Roles is null || !UserRolesExtensions.TryParse(r.Roles, out var roles) || roles == UserRoles.None)
            return Error.Validation("roles.invalid", "Roles must be a non-empty list of Parent, Staff, Admin.").ToProblem();
        if (id == me.UserId && !roles.HasFlag(UserRoles.Admin))
            return Error.Conflict("roles.cannot_remove_own_admin", "You cannot remove your own Admin role.").ToProblem();

        var u = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return Error.NotFound("user.not_found", "User not found.").ToProblem();
        var oldRoles = u.Roles;
        u.SetRoles(roles);
        await db.SaveChangesAsync(ct);
        await notices.RolesChangedAsync(u.Id, oldRoles, roles, ct);
        return Ok(ToDto(u, await CurrentDivisionAsync(u.Id, ct), await CurrentTeamsAsync(u.Id, ct)));
    }

    [HttpPut("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, SetActiveRequest r, CancellationToken ct)
    {
        if (id == me.UserId && !r.IsActive)
            return Error.Conflict("user.cannot_deactivate_self", "You cannot deactivate yourself.").ToProblem();

        var u = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return Error.NotFound("user.not_found", "User not found.").ToProblem();
        var wasActive = u.IsActive;
        u.SetActive(r.IsActive);

        if (!r.IsActive)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var t in await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAtUtc == null).ToListAsync(ct))
                t.Revoke(now);
        }
        await db.SaveChangesAsync(ct);
        if (wasActive != r.IsActive) await notices.ActiveChangedAsync(u.Id, r.IsActive, ct);
        if (wasActive && !r.IsActive)
            await Safe.RunAsync(log, "user deactivated", () => events.DispatchAsync([new Bob1.Shared.Contracts.Identity.UserDeactivated(u.Id)], ct));
        return NoContent();
    }

    // Note: reuses SetActiveRequest, so the body field is still called "isActive" (unchanged from the minimal API).
    [HttpPut("{id:guid}/suspended-from-participation")]
    public async Task<IActionResult> SetSuspendedFromParticipation(Guid id, SetActiveRequest r, CancellationToken ct)
    {
        var u = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return Error.NotFound("user.not_found", "User not found.").ToProblem();
        var wasSuspended = u.SuspendedFromParticipation;
        u.SetSuspendedFromParticipation(r.IsActive);
        await db.SaveChangesAsync(ct);
        if (wasSuspended != r.IsActive) await notices.ParticipationChangedAsync(u.Id, r.IsActive, ct);
        return NoContent();
    }

    private async Task<List<Guid>?> CurrentTeamsAsync(Guid userId, CancellationToken ct)
    {
        var season = await memberships.EnsureCurrentSeasonAsync(ct);
        return (await memberships.TeamsAsync(season, [userId], ct)).GetValueOrDefault(userId);
    }

    private async Task<Guid?> CurrentDivisionAsync(Guid userId, CancellationToken ct)
    {
        var season = await memberships.EnsureCurrentSeasonAsync(ct);
        return (await memberships.DivisionsAsync(season, [userId], ct)) is var d && d.TryGetValue(userId, out var id) ? id : null;
    }

    /// <summary>A person's divisions season by season (newest first): the club "parcours".</summary>
    [HttpQuery("{id:guid}/memberships")]
    public async Task<IActionResult> Memberships(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id, ct)) return Error.NotFound("user.not_found", "User not found.").ToProblem();
        var rows = await db.Memberships.AsNoTracking().Where(m => m.UserId == id).OrderByDescending(m => m.Season).ToListAsync(ct);
        var names = await club.GetDivisionNamesAsync(rows.Select(m => m.DivisionId), ct);
        var teamRows = await db.TeamMemberships.AsNoTracking().Where(t => t.UserId == id).ToListAsync(ct);
        var teamNames = await club.GetTeamNamesAsync(teamRows.Select(t => t.TeamId), ct);
        return Ok(rows.Select(m => new MembershipDto(m.Season, m.DivisionId, names.GetValueOrDefault(m.DivisionId, "?"),
            teamRows.Where(t => t.Season == m.Season)
                .Select(t => new TeamRef(t.TeamId, teamNames.GetValueOrDefault(t.TeamId, "?"))).ToArray())).ToList());
    }

    private static PersonDto ToDto(Domain.User u, Guid? divisionId, IEnumerable<Guid>? teamIds) =>
        new(u.Id, u.Email, u.FirstName, u.LastName, u.Roles.ToNames(), u.IsActive, u.SuspendedFromParticipation, u.FamilyId,
            divisionId, teamIds?.ToArray() ?? []);

    private static string NewTemporaryPassword() =>
        $"Bob1-{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}!aA";
}
