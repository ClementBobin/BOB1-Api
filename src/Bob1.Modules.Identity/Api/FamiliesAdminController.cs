using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bob1.Modules.Identity.Api;

internal sealed record AdminFamilyMemberDto(Guid Id, string FirstName, string LastName, string Email, string[] Roles, bool IsActive);
internal sealed record AdminFamilyDto(Guid Id, string Name, IReadOnlyList<AdminFamilyMemberDto> Members, DateTime CreatedAtUtc);
internal sealed record AdminFamilyRequest(string Name);

internal sealed record FamilyQuery(string? Search, bool? HideInactive, int? Skip, int? Take);

[Route(ApiRoutes.V1 + "/admin/families")]
[Authorize(Policy = Policies.Admin)]
[Tags("Families (admin)")]
internal sealed class FamiliesAdminController(IdentityDbContext db, TimeProvider clock) : ApiControllerBase
{
    [HttpQuery]
    public async Task<IActionResult> List([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] FamilyQuery? query, CancellationToken ct)
    {
        query ??= new(null, null, null, null);
        var q = db.Families.AsNoTracking();
        if (Paging.Term(query.Search) is { } s)
            q = q.Where(f => f.Name.ToLower().Contains(s)
                             || db.Users.Any(u => u.FamilyId == f.Id && (u.FirstName.ToLower().Contains(s) || u.LastName.ToLower().Contains(s))));
        if (query.HideInactive == true)
            q = q.Where(f => !db.Users.Any(u => u.FamilyId == f.Id && !u.IsActive));

        var total = await q.CountAsync(ct);
        var families = await q.OrderBy(f => f.Name)
            .Skip(Paging.Skip(query.Skip)).Take(Paging.Take(query.Take, 100, 300)).ToListAsync(ct);

        var ids = families.Select(f => f.Id).ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => u.FamilyId != null && ids.Contains(u.FamilyId.Value)).ToListAsync(ct);

        var usersByFamily = users.ToLookup(u => u.FamilyId!.Value);

        var items = families.Select(f => new AdminFamilyDto(
            f.Id, f.Name,
            usersByFamily[f.Id].OrderBy(u => u.FirstName)
                .Select(u => new AdminFamilyMemberDto(u.Id, u.FirstName, u.LastName, u.Email,
                    u.Roles.ToNames(), u.IsActive)).ToList(),
            f.CreatedAtUtc)).ToList();

        return Ok(new { total, items });
    }

    [HttpPost]
    public async Task<IActionResult> Create(AdminFamilyRequest r, CancellationToken ct)
    {
        if (Guard.NotBlank(r.Name, "name", 150) is { } err) return err.ToProblem();
        var name = r.Name.Trim();
        if (await db.Families.AnyAsync(f => f.Name == name, ct))
            return Error.Conflict("family.name_taken", "A family with this name already exists.").ToProblem();

        var family = Family.Create(name, clock.GetUtcNow().UtcDateTime);
        db.Families.Add(family);
        await db.SaveChangesAsync(ct);
        return Created($"/api/v1/admin/families/{family.Id}",
            new AdminFamilyDto(family.Id, family.Name, [], family.CreatedAtUtc));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, AdminFamilyRequest r, CancellationToken ct)
    {
        if (Guard.NotBlank(r.Name, "name", 150) is { } err) return err.ToProblem();
        var family = await db.Families.SingleOrDefaultAsync(f => f.Id == id, ct);
        if (family is null) return Error.NotFound("family.not_found", "Family not found.").ToProblem();

        var name = r.Name.Trim();
        if (await db.Families.AnyAsync(f => f.Id != id && f.Name == name, ct))
            return Error.Conflict("family.name_taken", "A family with this name already exists.").ToProblem();

        family.Rename(name);
        await db.SaveChangesAsync(ct);
        return Ok(new AdminFamilyDto(family.Id, family.Name, [], family.CreatedAtUtc));
    }
}
