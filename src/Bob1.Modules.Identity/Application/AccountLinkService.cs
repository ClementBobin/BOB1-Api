using System.Net.Mail;
using Microsoft.AspNetCore.Identity;

namespace Bob1.Modules.Identity.Application;

internal sealed record IssuedAccountLink(string Token, DateTime ExpiresAtUtc);

/// <summary>What the public page may show before the person acts: enough to recognise their own account, no more.</summary>
internal sealed record AccountLinkInfo(string Purpose, string FirstName, string MaskedEmail, DateTime ExpiresAtUtc);

/// <summary>
/// One-time links an admin copies to a person (instead of sending an email) so that the person sets a new password or a new
/// sign-in email themselves, downloads their data (export) or has their account erased. An admin never types the new value, so nothing about the person is changed by the admin.
/// Rules: 24 h lifetime, single use, a new link cancels the previous one, only the token's SHA-256 is stored, and a successful use
/// signs the person out everywhere (refresh tokens revoked).
/// </summary>
internal sealed class AccountLinkService(IdentityDbContext db, IPasswordHasher<User> hasher, TokenService tokens,
    TimeProvider clock, PeopleNotices notices, PersonalDataService personalData)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    // One answer for unknown, expired, already used and wrong-purpose tokens: a guess learns nothing.
    private static readonly Error InvalidLink =
        Error.NotFound("link.invalid", "This link is invalid, has expired or was already used.");

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<Result<IssuedAccountLink>> IssueAsync(Guid userId, AccountLinkPurpose purpose, Guid adminId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Error.NotFound("user.not_found", "User not found.");
        // A person who left the club still has the right to their data: only the account-changing links need an active account.
        if (!user.IsActive && purpose is AccountLinkPurpose.PasswordReset or AccountLinkPurpose.EmailChange)
            return Error.Conflict("user.inactive", "This account is deactivated.");

        var now = Now;
        // One live link per person and purpose: issuing a new one cancels the previous one.
        await db.AccountLinks.Where(l => l.UserId == userId && l.Purpose == purpose && l.UsedAtUtc == null && l.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAtUtc, now), ct);
        // Housekeeping: links that ended more than 30 days ago are of no use any more.
        await db.AccountLinks.Where(l => l.ExpiresAtUtc < now.AddDays(-30)).ExecuteDeleteAsync(ct);

        var (token, hash) = tokens.NewOpaqueToken();
        db.AccountLinks.Add(AccountLink.Create(userId, purpose, hash, adminId, now, Lifetime));
        await db.SaveChangesAsync(ct);
        return new IssuedAccountLink(token, now.Add(Lifetime));
    }

    public async Task<Result<AccountLinkInfo>> InspectAsync(string? token, CancellationToken ct)
    {
        var found = await FindAsync(token, purpose: null, ct);
        if (found is null) return InvalidLink;
        var (link, user) = found.Value;
        return new AccountLinkInfo(link.Purpose.ToString(), user.FirstName, Mask(user.Email), link.ExpiresAtUtc);
    }

    public async Task<Result> ResetPasswordAsync(string? token, string? newPassword, CancellationToken ct)
    {
        var found = await FindAsync(token, AccountLinkPurpose.PasswordReset, ct);
        if (found is null) return InvalidLink;
        var (link, user) = found.Value;

        // Checked against the person (their name and email must not be in it), before the link is used: a refused password costs nothing.
        var policy = PasswordPolicy.Check(newPassword, user.Email, user.FirstName, user.LastName);
        if (!policy.IsSuccess) return policy;
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, newPassword!) != PasswordVerificationResult.Failed)
            return Error.Validation("password.same_as_current", "The new password must differ from the current one.");

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            if (!await ClaimAsync(link.Id, ct)) return InvalidLink;   // used a moment ago by a parallel request
            user.SetPasswordHash(hasher.HashPassword(user, newPassword));
            await RevokeSessionsAsync(user.Id, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        await notices.PasswordChangedAsync(user.Id, ct);
        return Result.Success();
    }

    public async Task<Result> ChangeEmailAsync(string? token, string? newEmail, CancellationToken ct)
    {
        var email = (newEmail ?? "").Trim().ToLowerInvariant();
        if (email.Length > 200 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            return Error.Validation("email.invalid", "Invalid email address.");

        var found = await FindAsync(token, AccountLinkPurpose.EmailChange, ct);
        if (found is null) return InvalidLink;
        var (link, user) = found.Value;

        if (string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            return Error.Validation("email.unchanged", "This is already the email of the account.");
        if (await db.Users.AnyAsync(u => u.Id != user.Id && u.Email == email, ct))
            return Error.Conflict("email.taken", "An account with this email already exists.");

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (!await ClaimAsync(link.Id, ct)) return InvalidLink;
            user.SetEmail(email);
            await RevokeSessionsAsync(user.Id, ct);   // the access token carries the old email: sign in again
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())   // someone took the address in between
        {
            return Error.Conflict("email.taken", "An account with this email already exists.");
        }

        await notices.EmailChangedAsync(user.Id, ct);
        return Result.Success();
    }

    /// <summary>The person's whole data as one JSON-ready document. Single use, like every link: built first, then the link is claimed.</summary>
    public async Task<Result<object>> ExportDataAsync(string? token, CancellationToken ct)
    {
        var found = await FindAsync(token, AccountLinkPurpose.DataExport, ct);
        if (found is null) return InvalidLink;
        var (link, user) = found.Value;

        var export = await personalData.ExportAsync(user, ct);
        if (!await ClaimAsync(link.Id, ct)) return InvalidLink;   // used a moment ago by a parallel request
        return export;
    }

    /// <summary>
    /// Erases the account behind an AccountDeletion link. The person must type their full email (the page only shows it masked):
    /// the link alone is not enough for something that cannot be undone. A wrong email does not use the link up.
    /// </summary>
    public async Task<Result> DeleteAccountAsync(string? token, string? confirmEmail, CancellationToken ct)
    {
        var found = await FindAsync(token, AccountLinkPurpose.AccountDeletion, ct);
        if (found is null) return InvalidLink;
        var (_, user) = found.Value;

        if (!string.Equals(user.Email, (confirmEmail ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            return Error.Validation("email.mismatch", "The email does not match the account.");

        // No claim: the link is erased together with the account, and stays usable if a step fails and has to be retried.
        return await personalData.EraseAsync(user, ct);
    }

    /// <summary>The usable link behind a token (and its active owner), optionally of a given purpose.</summary>
    private async Task<(AccountLink Link, User User)?> FindAsync(string? token, AccountLinkPurpose? purpose, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = TokenService.Hash(token.Trim());
        var link = await db.AccountLinks.SingleOrDefaultAsync(l => l.TokenHash == hash, ct);
        if (link is null || !link.IsUsable(Now) || (purpose is { } p && link.Purpose != p)) return null;

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == link.UserId, ct);
        // Export and erasure also work for an account that was deactivated (someone who left the club).
        var keepsRights = link.Purpose is AccountLinkPurpose.DataExport or AccountLinkPurpose.AccountDeletion;
        return user is not null && (user.IsActive || keepsRights) ? (link, user) : null;
    }

    /// <summary>Atomically marks the link as used; false if it was already used or has expired (single use, even in a race).</summary>
    private async Task<bool> ClaimAsync(Guid linkId, CancellationToken ct)
    {
        var now = Now;
        var rows = await db.AccountLinks.Where(l => l.Id == linkId && l.UsedAtUtc == null && l.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.UsedAtUtc, (DateTime?)now), ct);
        return rows == 1;
    }

    private async Task RevokeSessionsAsync(Guid userId, CancellationToken ct)
    {
        var now = Now;
        foreach (var t in await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(ct))
            t.Revoke(now);
        foreach (var c in await db.BiometricCredentials.Where(c => c.UserId == userId && c.RevokedAtUtc == null).ToListAsync(ct))
            c.Revoke(now);
    }

    /// <summary>"camille@club.fr" -> "c***@club.fr".</summary>
    internal static string Mask(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }
}
