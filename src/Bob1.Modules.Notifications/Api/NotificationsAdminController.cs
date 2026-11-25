using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bob1.Modules.Notifications.Api;

internal sealed record NotificationQuery(string? Search, string? Type, int? Skip, int? Take);

/// <summary>One row of the admin history: a notification sent once, with how many people got it and how many read it.</summary>
internal sealed record AdminNotificationDto(Guid Id, string Title, string Body, string Type, string? DataJson,
    DateTime CreatedAtUtc, DateTime? ExpiresAtUtc, bool Recurring, bool Startup, int RecipientCount, int ReadCount,
    int? RecurrenceHours, DateTime? LastSentAtUtc);

internal sealed record NotificationRecipientDto(Guid UserId, DateTime DeliveredAtUtc, DateTime? ReadAtUtc);

[Route(ApiRoutes.V1 + "/admin/notifications")]
[Authorize(Policy = Policies.Admin)]
[Tags("Notifications (admin)")]
internal sealed class NotificationsAdminController(NotificationsDbContext db, ICurrentUser actor, TimeProvider clock) : ApiControllerBase
{
    private static readonly string[] BroadcastRoles = ["Parent", "Staff", "Admin"];
    private const int MaxRecurrenceHours = 24 * 90;

    /// <summary>A recurring notification needs an interval and an end date, so it never repeats forever.</summary>
    private Error? ValidateRecurrence(bool recurring, int? hours, DateTime? expiresAtUtc)
    {
        if (!recurring) return null;
        if (hours is not (>= 1 and <= MaxRecurrenceHours))
            return Error.Validation("notification.recurrence", $"Recurrence must be between 1 and {MaxRecurrenceHours} hours.");
        if (expiresAtUtc is not { } end || end <= clock.GetUtcNow().UtcDateTime)
            return Error.Validation("notification.recurrence_expiry", "A recurring notification needs an expiration date in the future.");
        return null;
    }

    // ---- Broadcast ----
    [HttpPost]
    public async Task<IActionResult> Broadcast(BroadcastRequest r,
        [FromServices] IIdentityApi identity, [FromServices] INotificationsApi notifications, CancellationToken ct)
    {
        if (Guard.First(Guard.NotBlank(r.Title, "title", 200), Guard.NotBlank(r.Body, "body", 1000)) is { } err)
            return err.ToProblem();
        if (ValidateRecurrence(r.Recurring, r.RecurrenceHours, r.ExpiresAtUtc) is { } recErr) return recErr.ToProblem();

        // A role targets everyone holding it (e.g. all Staff); a family its active members; a team its players (and families);
        // otherwise the division; otherwise everyone.
        IReadOnlyList<Guid> recipients;
        if (!string.IsNullOrWhiteSpace(r.Role))
        {
            var role = BroadcastRoles.FirstOrDefault(x => string.Equals(x, r.Role.Trim(), StringComparison.OrdinalIgnoreCase));
            if (role is null)
                return Error.Validation("notification.role", $"Unknown role. Allowed: {string.Join(", ", BroadcastRoles)}.").ToProblem();
            recipients = await identity.GetUserIdsByRoleAsync(role, ct);
        }
        else if (r.FamilyId is { } familyId)
            recipients = await identity.GetUserIdsOfFamilyAsync(familyId, ct);
        else if (r.TeamId is { } teamId)
            recipients = await identity.GetUserIdsByTeamsAsync([teamId], ct);
        else
            recipients = await identity.GetUserIdsByDivisionAsync(r.DivisionId, ct);
        if (recipients.Count == 0)
            return Error.Validation("notification.no_recipients", "Nobody belongs to this audience: nothing was sent.").ToProblem();
        var data = new Dictionary<string, string> { ["type"] = "broadcast" };

        // The one visible entry of the admin history.
        await notifications.SendToUsersAsync(recipients, r.Title.Trim(), r.Body.Trim(), data, ct, r.ExpiresAtUtc, r.Recurring, r.Startup,
            recurrenceHours: r.RecurrenceHours);

        // Admins who are not among the recipients get an in-app copy so they see what went out.
        // Hidden from the history: the broadcast above is already listed there.
        var admins = (await identity.GetUserIdsByRoleAsync("Admin", ct)).Except(recipients).Where(id => id != actor.UserId).ToList();
        await notifications.SendToUsersAsync(admins, "[Copie] " + r.Title.Trim(), r.Body.Trim(), data, ct, r.ExpiresAtUtc,
            push: false, adminVisible: false);
        return Ok(new { recipients = recipients.Count });
    }

    // ---- Audiences the admin can pick: only those with at least one recipient (divisions, teams, families, roles) ----
    [HttpGet("audiences")]
    public async Task<IActionResult> Audiences([FromServices] IIdentityApi identity, CancellationToken ct) =>
        Ok(await identity.GetAudienceSizesAsync(ct));

    // ---- History: one row per notification sent (personal notices are not listed) ----
    [HttpQuery]
    public async Task<IActionResult> List([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NotificationQuery? query, CancellationToken ct)
    {
        query ??= new(null, null, null, null);
        var q = db.Notifications.AsNoTracking().Where(n => n.VisibleInAdmin);
        if (Paging.Term(query.Search) is { } s)
            q = q.Where(n => n.Title.ToLower().Contains(s) || n.Body.ToLower().Contains(s));
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = query.Type.Trim();
            q = q.Where(n => n.Type == type);
        }
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(n => n.CreatedAtUtc)
            .Skip(Paging.Skip(query.Skip)).Take(Paging.Take(query.Take, 100, 500))
            .Select(n => new AdminNotificationDto(n.Id, n.Title, n.Body, n.Type, n.DataJson, n.CreatedAtUtc, n.ExpiresAtUtc,
                n.Recurring, n.Startup,
                db.UserNotifications.Count(u => u.NotificationId == n.Id),
                db.UserNotifications.Count(u => u.NotificationId == n.Id && u.ReadAtUtc != null),
                n.RecurrenceHours, n.LastSentAtUtc ?? n.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(new PagedResult<AdminNotificationDto>(total, rows));
    }

    // ---- Who received it / who read it ----
    [HttpGet("{id:guid}/recipients")]
    public async Task<IActionResult> Recipients(Guid id, CancellationToken ct)
    {
        if (!await db.Notifications.AnyAsync(n => n.Id == id && n.VisibleInAdmin, ct))
            return Error.NotFound("notification.not_found", "Notification not found.").ToProblem();

        var rows = await db.UserNotifications.AsNoTracking().Where(u => u.NotificationId == id)
            .OrderBy(u => u.ReadAtUtc == null).ThenBy(u => u.ReadAtUtc)
            .Select(u => new NotificationRecipientDto(u.UserId, u.CreatedAtUtc, u.ReadAtUtc))
            .ToListAsync(ct);
        return Ok(rows);
    }

    // Editing changes the single content row, so every recipient sees the update.
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, BroadcastRequest r, CancellationToken ct)
    {
        if (Guard.First(Guard.NotBlank(r.Title, "title", 200), Guard.NotBlank(r.Body, "body", 1000)) is { } err)
            return err.ToProblem();
        if (ValidateRecurrence(r.Recurring, r.RecurrenceHours, r.ExpiresAtUtc) is { } recErr) return recErr.ToProblem();

        var n = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.VisibleInAdmin, ct);
        if (n is null) return Error.NotFound("notification.not_found", "Notification not found.").ToProblem();

        n.Update(r.Title.Trim(), r.Body.Trim(), r.ExpiresAtUtc, r.Recurring, r.Startup, r.RecurrenceHours);
        await db.SaveChangesAsync(ct);
        return Ok(await ToDto(n.Id, ct));
    }

    // Deleting removes the content and, by cascade, every recipient's copy.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var n = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.VisibleInAdmin, ct);
        if (n is null) return Error.NotFound("notification.not_found", "Notification not found.").ToProblem();
        db.Notifications.Remove(n);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<AdminNotificationDto> ToDto(Guid id, CancellationToken ct) =>
        db.Notifications.AsNoTracking().Where(n => n.Id == id)
            .Select(n => new AdminNotificationDto(n.Id, n.Title, n.Body, n.Type, n.DataJson, n.CreatedAtUtc, n.ExpiresAtUtc,
                n.Recurring, n.Startup,
                db.UserNotifications.Count(u => u.NotificationId == n.Id),
                db.UserNotifications.Count(u => u.NotificationId == n.Id && u.ReadAtUtc != null),
                n.RecurrenceHours, n.LastSentAtUtc ?? n.CreatedAtUtc))
            .SingleAsync(ct);
}
