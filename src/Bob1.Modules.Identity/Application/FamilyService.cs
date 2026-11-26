using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Identity.Application;

internal sealed record CreateFamilyRequest(string? Name);
internal sealed record JoinFamilyRequest(string? Code);

/// <summary>
/// Creating, joining and leaving a family after sign-up. The family id travels in the access token, so every operation answers with a fresh
/// session: the app stores it and carries on without signing in again.
/// </summary>
internal sealed class FamilyService(IdentityDbContext db, AuthService auth, INotificationsApi notifications, IDomainEventDispatcher events,
    TimeProvider clock, ILogger<FamilyService> log)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private static Error AlreadyMember() =>
        Error.Conflict("family.already_member", "You already belong to a family: leave it first.");

    /// <summary>Only a parent starts a family; they become its first member.</summary>
    public async Task<Result<AuthResponse>> CreateAsync(Guid userId, string? name, CancellationToken ct)
    {
        var user = await ActiveUserAsync(userId, ct);
        if (user is null) return Error.Unauthorized("auth.invalid_credentials", "Account is not active.");
        if (!user.Roles.HasFlag(UserRoles.Parent)) return Error.Forbidden("family.parents_only", "Only parents can create a family.");
        if (user.FamilyId is not null) return AlreadyMember();

        var familyName = string.IsNullOrWhiteSpace(name) ? user.LastName : name.Trim();
        if (familyName.Length > 100) return Error.Validation("family_name.too_long", "The family name is too long (100 characters max).");

        var family = Family.Create(familyName, Now);
        db.Families.Add(family);
        user.SetFamilyId(family.Id);
        return await auth.IssueAsync(user, ct);
    }

    /// <summary>Anyone (parent or staff) can join a family with a parent's code; the code is single use.</summary>
    public async Task<Result<AuthResponse>> JoinAsync(Guid userId, string? code, CancellationToken ct)
    {
        var user = await ActiveUserAsync(userId, ct);
        if (user is null) return Error.Unauthorized("auth.invalid_credentials", "Account is not active.");
        if (user.FamilyId is not null) return AlreadyMember();

        var normalized = (code ?? "").Trim().ToUpperInvariant();
        var invitation = normalized.Length == 0 ? null : await db.Invitations.SingleOrDefaultAsync(i => i.Code == normalized, ct);
        if (invitation is null || !invitation.IsUsable(Now))
            return Error.Validation("invitation.invalid", "This invitation code is invalid or expired.");

        invitation.MarkUsed(Now);
        user.SetFamilyId(invitation.FamilyId);
        var issued = await auth.IssueAsync(user, ct);
        if (issued.IsSuccess)
            await Safe.RunAsync(log, "family member joined", () => NotifyMembersAsync(user, invitation.FamilyId, "Nouveau membre dans la famille",
                $"{user.FirstName} {user.LastName} a rejoint votre famille.", "family_member_joined", ct));
        return issued;
    }

    /// <summary>
    /// Leaves the family. The last member out deletes it (and its invitations), and Participation drops its presence confirmations;
    /// otherwise the remaining members are told. The person's own signups, points and memberships are theirs and stay.
    /// </summary>
    public async Task<Result<AuthResponse>> LeaveAsync(Guid userId, CancellationToken ct)
    {
        var user = await ActiveUserAsync(userId, ct);
        if (user is null) return Error.Unauthorized("auth.invalid_credentials", "Account is not active.");
        if (user.FamilyId is not { } familyId) return Error.Conflict("family.none", "You are not in a family.");

        // Deactivated accounts still belong to the family: only a family with nobody at all is deleted.
        var anyoneElse = await db.Users.AnyAsync(u => u.FamilyId == familyId && u.Id != user.Id, ct);
        user.SetFamilyId(null);
        if (!anyoneElse) await StageRemovalAsync(familyId, ct);

        var issued = await auth.IssueAsync(user, ct);
        if (!issued.IsSuccess) return issued;

        if (!anyoneElse)
            await Safe.RunAsync(log, "family dissolved", () => events.DispatchAsync([new FamilyDissolved(familyId)], ct));
        else
            await Safe.RunAsync(log, "family member left", () => NotifyMembersAsync(user, familyId, "Un membre a quitté la famille",
                $"{user.FirstName} {user.LastName} a quitté votre famille.", "family_member_left", ct));
        return issued;
    }

    /// <summary>
    /// Deletes the family (and its invitations) when nobody belongs to it any more. For paths that move a person out of a family without
    /// going through <see cref="LeaveAsync"/>, e.g. an admin changing someone's family. Returns true when the family was deleted.
    /// </summary>
    public async Task<bool> DissolveIfEmptyAsync(Guid familyId, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.FamilyId == familyId, ct)) return false;
        await StageRemovalAsync(familyId, ct);
        await db.SaveChangesAsync(ct);
        await Safe.RunAsync(log, "family dissolved", () => events.DispatchAsync([new FamilyDissolved(familyId)], ct));
        return true;
    }

    private async Task StageRemovalAsync(Guid familyId, CancellationToken ct)
    {
        db.Invitations.RemoveRange(await db.Invitations.Where(i => i.FamilyId == familyId).ToListAsync(ct));
        var family = await db.Families.SingleOrDefaultAsync(f => f.Id == familyId, ct);
        if (family is not null) db.Families.Remove(family);
    }

    private Task<User?> ActiveUserAsync(Guid userId, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);

    /// <summary>In-app notice (no push) to the active members of the family, except the person it is about.</summary>
    private async Task NotifyMembersAsync(User about, Guid familyId, string title, string body, string type, CancellationToken ct)
    {
        var members = await db.Users.AsNoTracking().Where(u => u.FamilyId == familyId && u.IsActive && u.Id != about.Id)
            .Select(u => u.Id).ToListAsync(ct);
        if (members.Count == 0) return;
        await notifications.SendToUsersAsync(members, title, body,
            new Dictionary<string, string> { ["type"] = type, ["personId"] = about.Id.ToString() }, ct, push: false);
    }
}
