using Microsoft.AspNetCore.Identity;

namespace Bob1.Modules.Identity.Application;

/// <summary>
/// <paramref name="IsParent"/>: false registers a staff member (official, table crew...) instead of a parent. The family is optional:
/// an <paramref name="InvitationCode"/> joins one, <paramref name="CreateFamily"/> (parents only, the default of older apps) creates one,
/// otherwise the account has no family yet and can create or join one later (<c>/family</c>).
/// </summary>
internal sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName, string? FamilyName, string? InvitationCode,
    bool IsParent = true, bool CreateFamily = true);
internal sealed record LoginRequest(string Email, string Password);
internal sealed record RefreshRequest(string RefreshToken);
internal sealed record BiometricEnrollRequest(string? DeviceName);
internal sealed record BiometricEnrollResponse(string BiometricToken, Guid CredentialId);
internal sealed record BiometricLoginRequest(string BiometricToken);
internal sealed record BiometricDisableRequest(Guid? CredentialId);

internal sealed record UserDto(Guid Id, string Email, string FirstName, string LastName, string[] Roles, Guid? FamilyId);
internal sealed record AuthResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds, UserDto User);

internal static class UserMapping
{
    public static UserDto ToDto(this User u) => new(u.Id, u.Email, u.FirstName, u.LastName, u.Roles.ToNames(), u.FamilyId);
}

internal sealed class AuthService(IdentityDbContext db, IPasswordHasher<User> hasher, TokenService tokens, TimeProvider clock,
    Bob1.Shared.Contracts.Notifications.INotificationsApi notifications, ILogger<AuthService> log)
{
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "Invalid email or password.");

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest r, CancellationToken ct)
    {
        var invalid = Guard.First(
            Guard.NotBlank(r.Email, "email"),
            Guard.NotBlank(r.FirstName, "first_name", 100),
            Guard.NotBlank(r.LastName, "last_name", 100));
        if (invalid is not null) return invalid;
        if (!r.Email.Contains('@')) return Error.Validation("email.invalid", "Invalid email address.");
        var policy = PasswordPolicy.Check(r.Password, r.Email.Trim(), r.FirstName.Trim(), r.LastName.Trim());
        if (!policy.IsSuccess) return policy.Error;
        if (!r.IsParent && !string.IsNullOrWhiteSpace(r.FamilyName))
            return Error.Validation("family.parents_only", "Only parents can create a family.");

        var email = r.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Error.Conflict("email.taken", "An account with this email already exists.");

        Guid? familyId = null;
        var joinedExistingFamily = false;
        if (!string.IsNullOrWhiteSpace(r.InvitationCode))
        {
            var code = r.InvitationCode.Trim().ToUpperInvariant();
            var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.Code == code, ct);
            if (invitation is null || !invitation.IsUsable(Now))
                return Error.Validation("invitation.invalid", "This invitation code is invalid or expired.");
            invitation.MarkUsed(Now);
            familyId = invitation.FamilyId;
            joinedExistingFamily = true;
        }
        else if (r.IsParent && r.CreateFamily)
        {
            var name = string.IsNullOrWhiteSpace(r.FamilyName) ? r.LastName.Trim() : r.FamilyName.Trim();
            var family = Family.Create(name, Now);
            db.Families.Add(family);
            familyId = family.Id;
        }

        var user = User.Create(email, r.FirstName.Trim(), r.LastName.Trim(), r.IsParent ? UserRoles.Parent : UserRoles.Staff, familyId, Now);
        user.SetPasswordHash(hasher.HashPassword(user, r.Password));
        db.Users.Add(user);

        try
        {
            var issued = await IssueAsync(user, ct);

            // A new member joined through an invitation: the existing members of the family are told (in-app).
            if (joinedExistingFamily && issued.IsSuccess)
                await Safe.RunAsync(log, "family member joined", async () =>
                {
                    var members = await db.Users.AsNoTracking().Where(u => u.FamilyId == familyId && u.IsActive && u.Id != user.Id)
                        .Select(u => u.Id).ToListAsync(ct);
                    await notifications.SendToUsersAsync(members, "Nouveau membre dans la famille",
                        $"{user.FirstName} {user.LastName} a rejoint votre famille.",
                        new Dictionary<string, string> { ["type"] = "family_member_joined", ["personId"] = user.Id.ToString() }, ct, push: false);
                });
            return issued;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation()) // UNIQUE / PRIMARY KEY
        {
            return Error.Conflict("email.taken", "An account with this email already exists.");
        }
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest r, CancellationToken ct)
    {
        var email = (r.Email ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !user.IsActive || string.IsNullOrEmpty(r.Password)) return InvalidCredentials;

        var check = hasher.VerifyHashedPassword(user, user.PasswordHash, r.Password);
        if (check == PasswordVerificationResult.Failed) return InvalidCredentials;
        if (check == PasswordVerificationResult.SuccessRehashNeeded)
            user.SetPasswordHash(hasher.HashPassword(user, r.Password));

        return await IssueAsync(user, ct);
    }

    /// <summary>Refresh-token rotation: the presented token is revoked and a new pair is issued.</summary>
    public async Task<Result<AuthResponse>> RefreshAsync(RefreshRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.RefreshToken)) return InvalidToken();
        var hash = TokenService.Hash(r.RefreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || !stored.IsActive(Now)) return InvalidToken();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null || !user.IsActive) return InvalidToken();

        stored.Revoke(Now);
        return await IssueAsync(user, ct);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = TokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return;
        stored.Revoke(Now);
        await db.SaveChangesAsync(ct);
    }

    // ---- Biometric sign-in -------------------------------------------------------------------------------------------

    /// <summary>Enrolls the calling user's device: returns the one and only copy of the raw token (the device stores it behind the biometric prompt).</summary>
    public async Task<Result<BiometricEnrollResponse>> EnrollBiometricAsync(Guid userId, BiometricEnrollRequest r, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive) return Error.Unauthorized("auth.invalid_credentials", "Account is not active.");

        var active = await db.BiometricCredentials.Where(c => c.UserId == userId && c.RevokedAtUtc == null && c.ExpiresAtUtc > Now)
            .OrderBy(c => c.CreatedAtUtc).ToListAsync(ct);
        // Keep the number of enrolled devices bounded: the oldest ones make room for the new one.
        foreach (var old in active.Take(Math.Max(0, active.Count - (BiometricCredential.MaxPerUser - 1))))
            old.Revoke(Now);

        var (token, hash) = tokens.NewOpaqueToken();
        var device = string.IsNullOrWhiteSpace(r.DeviceName) ? null : r.DeviceName.Trim();
        if (device is { Length: > 100 }) device = device[..100];
        var credential = BiometricCredential.Create(userId, hash, device, Now);
        db.BiometricCredentials.Add(credential);
        await db.SaveChangesAsync(ct);
        return new BiometricEnrollResponse(token, credential.Id);
    }

    /// <summary>Anonymous: trades a device's biometric token for a normal session (same response as /login).</summary>
    public async Task<Result<AuthResponse>> LoginBiometricAsync(BiometricLoginRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.BiometricToken)) return InvalidBiometricToken();
        var hash = TokenService.Hash(r.BiometricToken);
        var credential = await db.BiometricCredentials.SingleOrDefaultAsync(c => c.TokenHash == hash, ct);
        if (credential is null || !credential.IsActive(Now)) return InvalidBiometricToken();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == credential.UserId, ct);
        if (user is null || !user.IsActive) return InvalidBiometricToken();

        credential.Touch(Now);
        return await IssueAsync(user, ct);   // saves the credential's new expiry together with the refresh token
    }

    /// <summary>Disables biometric sign-in: the given credential (the device keeps its id next to the encrypted token), or every credential of the user when none is given.</summary>
    public async Task DisableBiometricAsync(Guid userId, Guid? credentialId, CancellationToken ct)
    {
        var query = db.BiometricCredentials.Where(c => c.UserId == userId && c.RevokedAtUtc == null);
        if (credentialId is { } id) query = query.Where(c => c.Id == id);
        foreach (var c in await query.ToListAsync(ct)) c.Revoke(Now);
        await db.SaveChangesAsync(ct);
    }

    private static Error InvalidBiometricToken() =>
        Error.Unauthorized("auth.invalid_biometric_token", "Biometric sign-in is no longer valid on this device. Sign in with your password.");

    private static Error InvalidToken() => Error.Unauthorized("auth.invalid_refresh_token", "Invalid or expired refresh token.");

    /// <summary>A new session for the user (also used when their family changes: the family id travels in the access token).</summary>
    internal async Task<Result<AuthResponse>> IssueAsync(User user, CancellationToken ct)
    {
        var (token, hash) = tokens.NewRefreshToken();
        db.RefreshTokens.Add(RefreshToken.Create(user.Id, hash, Now.Add(TokenService.RefreshLifetime), Now));
        await db.SaveChangesAsync(ct);

        return new AuthResponse(tokens.CreateAccessToken(user), token, (int)TokenService.AccessLifetime.TotalSeconds, user.ToDto());
    }
}
