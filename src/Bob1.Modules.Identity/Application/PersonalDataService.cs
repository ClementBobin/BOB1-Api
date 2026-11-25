using Bob1.Shared.Contracts.Privacy;

namespace Bob1.Modules.Identity.Application;

/// <summary>
/// GDPR access / portability (export) and erasure of one person. Identity owns the account, family and memberships; every other
/// module adds its own part through <see cref="IPersonalDataContributor"/>.
/// </summary>
internal sealed class PersonalDataService(IdentityDbContext db, IEnumerable<IPersonalDataContributor> contributors,
    IDomainEventDispatcher events, TimeProvider clock, ILogger<PersonalDataService> log)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<object> ExportAsync(User user, CancellationToken ct)
    {
        var subject = await SubjectAsync(user, ct);

        var family = user.FamilyId is { } fid
            ? await db.Families.AsNoTracking().Where(f => f.Id == fid)
                .Select(f => new { f.Name, f.CreatedAtUtc, Members = db.Users.Count(u => u.FamilyId == fid) }).SingleOrDefaultAsync(ct)
            : null;
        var memberships = await db.Memberships.AsNoTracking().Where(m => m.UserId == user.Id)
            .OrderBy(m => m.Season).Select(m => new { m.Season, m.DivisionId, m.CreatedAtUtc }).ToListAsync(ct);
        var teamMemberships = await db.TeamMemberships.AsNoTracking().Where(m => m.UserId == user.Id)
            .OrderBy(m => m.Season).Select(m => new { m.Season, m.TeamId }).ToListAsync(ct);
        var biometricDevices = await db.BiometricCredentials.AsNoTracking().Where(c => c.UserId == user.Id)
            .OrderBy(c => c.CreatedAtUtc).Select(c => new { c.DeviceName, c.CreatedAtUtc, c.LastUsedAtUtc, c.RevokedAtUtc }).ToListAsync(ct);

        var sections = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var c in contributors) sections[c.Section] = await c.ExportAsync(subject, ct);

        return new
        {
            generatedAtUtc = Now,
            note = "Données personnelles détenues par l'application BOB1 pour ce compte. Les mots de passe ne sont jamais conservés en clair.",
            account = new
            {
                user.Id, user.Email, user.FirstName, user.LastName,
                roles = user.Roles.ToNames(), user.IsActive, user.SuspendedFromParticipation, user.CreatedAtUtc
            },
            family,
            memberships,
            teamMemberships,
            biometricDevices,
            data = sections
        };
    }

    /// <summary>
    /// Erases the person for good. Order matters: the account is closed first (so nothing can sign in while this runs and a
    /// failure leaves a safe, retryable state), the organisers are told about the holes, every module deletes its part, then
    /// the identity rows go. Every step is idempotent, so the same link can be used again if something failed halfway.
    /// </summary>
    public async Task<Result> EraseAsync(User user, CancellationToken ct)
    {
        if (user.Roles.HasFlag(UserRoles.Admin) && !await db.Users.AnyAsync(u => u.Id != user.Id && u.IsActive && (u.Roles & UserRoles.Admin) != 0, ct))
            return Error.Conflict("user.last_admin", "The last administrator cannot be erased: give the Admin role to someone else first.");

        var subject = await SubjectAsync(user, ct);
        var wasActive = user.IsActive;

        // 1. Close the account and every session.
        var now = Now;
        user.SetActive(false);
        foreach (var t in await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAtUtc == null).ToListAsync(ct)) t.Revoke(now);
        foreach (var c in await db.BiometricCredentials.Where(c => c.UserId == user.Id && c.RevokedAtUtc == null).ToListAsync(ct)) c.Revoke(now);
        await db.SaveChangesAsync(ct);

        // 2. Slots and rides leave holes: reuse the deactivation notices (admins, passengers) while the person still exists.
        if (wasActive)
            await Safe.RunAsync(log, "user erased (deactivated)", () => events.DispatchAsync([new Bob1.Shared.Contracts.Identity.UserDeactivated(user.Id)], ct));

        // 3. Every other module.
        foreach (var c in contributors) await c.EraseAsync(subject, ct);

        // 4. Identity: one transaction, the user row last.
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.RefreshTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync(ct);
            await db.BiometricCredentials.Where(c => c.UserId == user.Id).ExecuteDeleteAsync(ct);
            await db.AccountLinks.Where(l => l.UserId == user.Id).ExecuteDeleteAsync(ct);
            await db.Memberships.Where(m => m.UserId == user.Id).ExecuteDeleteAsync(ct);
            await db.TeamMemberships.Where(m => m.UserId == user.Id).ExecuteDeleteAsync(ct);
            await db.Invitations.Where(i => i.CreatedByUserId == user.Id).ExecuteDeleteAsync(ct);
            if (subject.FamilyEmptied && subject.FamilyId is { } familyId)
            {
                await db.Invitations.Where(i => i.FamilyId == familyId).ExecuteDeleteAsync(ct);
                await db.Families.Where(f => f.Id == familyId).ExecuteDeleteAsync(ct);
            }
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
        }

        db.ChangeTracker.Clear();
        log.LogInformation("Account {UserId} erased on the person's request", subject.UserId);   // an id only: no name, no email
        return Result.Success();
    }

    private async Task<PersonalDataSubject> SubjectAsync(User user, CancellationToken ct)
    {
        var emptied = user.FamilyId is { } fid && !await db.Users.AnyAsync(u => u.FamilyId == fid && u.Id != user.Id, ct);
        return new PersonalDataSubject(user.Id, user.FamilyId, emptied);
    }
}
