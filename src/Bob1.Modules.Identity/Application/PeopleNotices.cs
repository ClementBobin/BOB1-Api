using Bob1.Shared.Contracts.Club;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Identity.Application;

/// <summary>
/// Notifications about accounts: the person is told (push + in-app), the family when it concerns them, and the other
/// admins get an in-app audit copy. Failures are logged: they never undo the admin's change.
/// </summary>
internal sealed class PeopleNotices(
    INotificationsApi notifications, IClubApi club, IdentityDbContext db, ICurrentUser actor, ILogger<PeopleNotices> log)
{
    private Guid? Actor => actor.IsAuthenticated ? actor.UserId : null;

    private static Dictionary<string, string> Data(string type, Guid userId) =>
        new() { ["type"] = type, ["personId"] = userId.ToString() };

    private async Task<string> NameAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => new { x.FirstName, x.LastName }).SingleOrDefaultAsync(ct);
        return u is null ? "?" : $"{u.FirstName} {u.LastName}".Trim();
    }

    private Task<List<Guid>> FamilyAsync(Guid? familyId, CancellationToken ct) =>
        familyId is null ? Task.FromResult(new List<Guid>())
            : db.Users.AsNoTracking().Where(u => u.FamilyId == familyId && u.IsActive).Select(u => u.Id).ToListAsync(ct);

    /// <summary>Division and/or team changed (current season).</summary>
    public Task ClubChangedAsync(Guid userId, Guid? oldDivision, Guid? newDivision,
        IReadOnlyCollection<Guid> oldTeams, IReadOnlyCollection<Guid> newTeams, CancellationToken ct) =>
        Safe.RunAsync(log, "club change", async () =>
        {
            var divisionChanged = oldDivision != newDivision;
            var teamsChanged = !oldTeams.OrderBy(x => x).SequenceEqual(newTeams.OrderBy(x => x));
            if (!divisionChanged && !teamsChanged) return;

            var divisions = await club.GetDivisionNamesAsync(new[] { oldDivision, newDivision }.OfType<Guid>(), ct);
            var teams = await club.GetTeamNamesAsync(oldTeams.Concat(newTeams), ct);
            string D(Guid? id) => id is { } g ? divisions.GetValueOrDefault(g, "?") : "aucune";
            string T(IEnumerable<Guid> ids) => ids.Any() ? string.Join(", ", ids.Select(i => teams.GetValueOrDefault(i, "?"))) : "aucune";

            var parts = new List<string>();
            if (divisionChanged) parts.Add($"division {D(oldDivision)} → {D(newDivision)}");
            if (teamsChanged) parts.Add($"équipe(s) {T(oldTeams)} → {T(newTeams)}");
            var change = string.Join(" ; ", parts);

            var name = await NameAsync(userId, ct);
            var familyId = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.FamilyId).SingleOrDefaultAsync(ct);
            var recipients = (await FamilyAsync(familyId, ct)).Append(userId).Distinct().ToList();
            await notifications.SendToUsersAsync(recipients, "Changement d'équipe", $"{name} : {change}.", Data("person_club_changed", userId), ct);
            await notifications.NotifyAdminsAsync("Changement d'équipe", $"{name} : {change}.", Data("person_club_changed", userId), Actor, ct);
        });

    public Task FamilyChangedAsync(Guid userId, Guid? oldFamily, Guid? newFamily, CancellationToken ct) =>
        oldFamily == newFamily ? Task.CompletedTask : Safe.RunAsync(log, "family change", async () =>
        {
            var name = await NameAsync(userId, ct);
            var recipients = (await FamilyAsync(oldFamily, ct)).Concat(await FamilyAsync(newFamily, ct)).Append(userId).Distinct().ToList();
            var body = newFamily is null ? $"{name} ne fait plus partie de la famille." : $"{name} a changé de famille.";
            await notifications.SendToUsersAsync(recipients, "Changement de famille", body, Data("person_family_changed", userId), ct);
            await notifications.NotifyAdminsAsync("Changement de famille", body, Data("person_family_changed", userId), Actor, ct);
        });

    public Task RolesChangedAsync(Guid userId, UserRoles oldRoles, UserRoles newRoles, CancellationToken ct) =>
        oldRoles == newRoles ? Task.CompletedTask : Safe.RunAsync(log, "roles change", async () =>
        {
            var name = await NameAsync(userId, ct);
            string R(UserRoles r) => string.Join(", ", r.ToNames());
            var body = $"Rôles : {R(oldRoles)} → {R(newRoles)}.";
            await notifications.SendToPersonAsync(userId, "Vos rôles ont changé", body, Data("person_role_changed", userId), ct);
            await notifications.NotifyAdminsAsync("Changement de rôle", $"{name} : {body}", Data("person_role_changed", userId), Actor, ct);
        });

    /// <summary>Account enabled / disabled.</summary>
    public Task ActiveChangedAsync(Guid userId, bool active, CancellationToken ct) =>
        Safe.RunAsync(log, "account status", async () =>
        {
            var name = await NameAsync(userId, ct);
            var body = active ? "Votre compte a été réactivé." : "Votre compte a été désactivé.";
            await notifications.SendToPersonAsync(userId, active ? "Compte réactivé" : "Compte désactivé", body, Data("person_status_changed", userId), ct);
            await notifications.NotifyAdminsAsync(active ? "Compte réactivé" : "Compte désactivé", $"{name} : {body}",
                Data("person_status_changed", userId), Actor, ct);
        });

    /// <summary>Suspended / restored for participation: the person and the family are told.</summary>
    public Task ParticipationChangedAsync(Guid userId, bool suspended, CancellationToken ct) =>
        Safe.RunAsync(log, "participation status", async () =>
        {
            var name = await NameAsync(userId, ct);
            var familyId = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.FamilyId).SingleOrDefaultAsync(ct);
            var recipients = (await FamilyAsync(familyId, ct)).Append(userId).Distinct().ToList();
            var title = suspended ? "Participation suspendue" : "Participation rétablie";
            var body = suspended
                ? $"{name} est suspendu(e) de la participation aux matchs."
                : $"{name} peut de nouveau participer aux matchs.";
            await notifications.SendToUsersAsync(recipients, title, body, Data("person_participation_changed", userId), ct);
            await notifications.NotifyAdminsAsync(title, body, Data("person_participation_changed", userId), Actor, ct);
        });

    /// <summary>Security notice (email is not implemented: in-app + push only for now).</summary>
    public Task PasswordChangedAsync(Guid userId, CancellationToken ct) =>
        Safe.RunAsync(log, "password changed", () => notifications.SendToPersonAsync(userId, "Mot de passe modifié",
            "Votre mot de passe vient d'être modifié. Si ce n'est pas vous, contactez un administrateur.",
            Data("security_password_changed", userId), ct));
}
