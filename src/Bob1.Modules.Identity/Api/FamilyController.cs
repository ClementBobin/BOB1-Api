using System.Security.Cryptography;
using Bob1.Shared.Contracts.Club;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bob1.Modules.Identity.Api;

internal sealed record MemberDto(Guid UserId, string FirstName, string LastName, string Email, bool IsMe, Guid? DivisionId, string? DivisionName, Guid[] TeamIds);
internal sealed record FamilyDto(Guid Id, string Name, IReadOnlyList<MemberDto> Members);
internal sealed record InvitationDto(string Code, DateTime ExpiresAtUtc);

/// <summary>
/// The family of the signed-in person. Anyone with a family can see it, join one with a code, or leave; only a parent starts a family
/// or hands out codes. Creating / joining / leaving answers with a fresh session (the family id is part of the access token).
/// </summary>
[Route(ApiRoutes.V1 + "/family")]
[Authorize]
[Tags("Family")]
internal sealed class FamilyController(IdentityDbContext db, IClubApi club, MembershipService memberships, FamilyService families,
    ICurrentUser user, TimeProvider clock) : ApiControllerBase
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (user.FamilyId is not { } familyId) return Error.Forbidden("family.none", "No family.").ToProblem();

        var f = await db.Families.AsNoTracking().SingleOrDefaultAsync(x => x.Id == familyId, ct);
        if (f is null) return Error.NotFound("family.not_found", "Family not found.").ToProblem();

        var rows = await db.Users.AsNoTracking().Where(u => u.FamilyId == familyId).OrderBy(u => u.FirstName).ToListAsync(ct);
        var season = await memberships.EnsureCurrentSeasonAsync(ct);
        var memberDivisions = await memberships.DivisionsAsync(season, rows.Select(u => u.Id), ct);
        var memberTeams = await memberships.TeamsAsync(season, rows.Select(u => u.Id), ct);
        var divisions = await club.GetDivisionNamesAsync(memberDivisions.Values, ct);
        var members = rows.Select(u =>
        {
            Guid? divisionId = memberDivisions.TryGetValue(u.Id, out var d) ? d : null;
            return new MemberDto(u.Id, u.FirstName, u.LastName, u.Email, u.Id == user.UserId,
                divisionId, divisionId is { } id && divisions.TryGetValue(id, out var n) ? n : null,
                memberTeams.TryGetValue(u.Id, out var teams) ? teams.ToArray() : []);
        }).ToList();
        return Ok(new FamilyDto(f.Id, f.Name, members));
    }

    /// <summary>Starts a family (parents only; the person must not already be in one). The name defaults to the last name.</summary>
    [HttpPost, Authorize(Policy = Policies.Parent)]
    public async Task<IActionResult> Create(CreateFamilyRequest r, CancellationToken ct)
    {
        var result = await families.CreateAsync(user.UserId, r.Name, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>Joins the family behind a parent's code (single use, 7 days).</summary>
    [HttpPost("join")]
    public async Task<IActionResult> Join(JoinFamilyRequest r, CancellationToken ct)
    {
        var result = await families.JoinAsync(user.UserId, r.Code, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>Leaves the family. The last member out deletes it.</summary>
    [HttpPost("leave")]
    public async Task<IActionResult> Leave(CancellationToken ct)
    {
        var result = await families.LeaveAsync(user.UserId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblem();
    }

    [HttpPost("invitations"), Authorize(Policy = Policies.Parent)]
    public async Task<IActionResult> CreateInvitation(CancellationToken ct)
    {
        if (user.FamilyId is not { } familyId) return Error.Forbidden("family.none", "No family.").ToProblem();
        var invitation = FamilyInvitation.Create(
            familyId, RandomNumberGenerator.GetString(CodeAlphabet, 8), user.UserId,
            clock.GetUtcNow().UtcDateTime.AddDays(7));
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(ct);
        return Ok(new InvitationDto(invitation.Code, invitation.ExpiresAtUtc));
    }
}
