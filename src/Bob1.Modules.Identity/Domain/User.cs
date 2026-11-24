namespace Bob1.Modules.Identity.Domain;

[Flags]
internal enum UserRoles { None = 0, Parent = 1, Staff = 2, Admin = 4 }

internal static class UserRolesExtensions
{
    public static string[] ToNames(this UserRoles roles) =>
        Enum.GetValues<UserRoles>().Where(r => r != UserRoles.None && roles.HasFlag(r)).Select(r => r.ToString()).ToArray();

    public static bool TryParse(IEnumerable<string> names, out UserRoles roles)
    {
        roles = UserRoles.None;
        foreach (var name in names)
        {
            if (!Enum.TryParse<UserRoles>(name, ignoreCase: true, out var r) || r == UserRoles.None)
                return false;
            roles |= r;
        }
        return true;
    }
}

internal sealed class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public UserRoles Roles { get; private set; }
    public Guid? FamilyId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool SuspendedFromParticipation { get; private set; } = false;
    public DateTime CreatedAtUtc { get; private set; }

    public string DisplayName => $"{FirstName} {LastName}".Trim();

    private User() { }

    public static User Create(string email, string firstName, string lastName, UserRoles roles, Guid? familyId, DateTime nowUtc) =>
        new() { Email = email, FirstName = firstName, LastName = lastName, Roles = roles, FamilyId = familyId, CreatedAtUtc = nowUtc };

    public void SetPasswordHash(string hash) => PasswordHash = hash;
    public void SetName(string firstName, string lastName) { FirstName = firstName; LastName = lastName; }
    public void SetEmail(string email) => Email = email;
    public void SetFamilyId(Guid? familyId) => FamilyId = familyId;
    public void SetRoles(UserRoles roles) => Roles = roles;
    public void SetActive(bool active) => IsActive = active;
    public void SetSuspendedFromParticipation(bool suspended) => SuspendedFromParticipation = suspended;
}

internal sealed class Family
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }

    private Family() { }

    public static Family Create(string name, DateTime nowUtc) => new() { Name = name, CreatedAtUtc = nowUtc };
    public void Rename(string name) => Name = name;
}

internal sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTime expiresAtUtc, DateTime nowUtc) =>
        new() { UserId = userId, TokenHash = tokenHash, ExpiresAtUtc = expiresAtUtc, CreatedAtUtc = nowUtc };

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
    public void Revoke(DateTime nowUtc) => RevokedAtUtc ??= nowUtc;
}

internal sealed class FamilyInvitation
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public string Code { get; private set; } = default!;
    public Guid CreatedByUserId { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }

    private FamilyInvitation() { }

    public static FamilyInvitation Create(Guid familyId, string code, Guid createdBy, DateTime expiresAtUtc) =>
        new() { FamilyId = familyId, Code = code, CreatedByUserId = createdBy, ExpiresAtUtc = expiresAtUtc };

    public bool IsUsable(DateTime nowUtc) => UsedAtUtc is null && ExpiresAtUtc > nowUtc;
    public void MarkUsed(DateTime nowUtc) => UsedAtUtc = nowUtc;
}

internal enum AccountLinkPurpose { PasswordReset = 1, EmailChange = 2 }

/// <summary>
/// A one-time, expiring link an admin hands to a person (copy / paste) instead of an email, so the person can set a new
/// password or a new sign-in email themselves. Only the SHA-256 hash of the token is stored.
/// </summary>
internal sealed class AccountLink
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public AccountLinkPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }

    private AccountLink() { }

    public static AccountLink Create(Guid userId, AccountLinkPurpose purpose, string tokenHash, Guid createdBy,
        DateTime nowUtc, TimeSpan lifetime) =>
        new() { UserId = userId, Purpose = purpose, TokenHash = tokenHash, CreatedByUserId = createdBy,
            CreatedAtUtc = nowUtc, ExpiresAtUtc = nowUtc.Add(lifetime) };

    public bool IsUsable(DateTime nowUtc) => UsedAtUtc is null && ExpiresAtUtc > nowUtc;
}

/// <summary>
/// A device enrolled for biometric sign-in. The raw token lives on the device, encrypted by a key that only unlocks after a
/// fingerprint / face check; only its SHA-256 hash is stored here. Unlike a refresh token it is not rotated, so it stays
/// valid for the device until revoked, expired (sliding window) or the password changes.
/// </summary>
internal sealed class BiometricCredential
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(90);
    public const int MaxPerUser = 5;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public string? DeviceName { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private BiometricCredential() { }

    public static BiometricCredential Create(Guid userId, string tokenHash, string? deviceName, DateTime nowUtc) =>
        new() { UserId = userId, TokenHash = tokenHash, DeviceName = deviceName, CreatedAtUtc = nowUtc, ExpiresAtUtc = nowUtc.Add(Lifetime) };

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
    public void Revoke(DateTime nowUtc) => RevokedAtUtc ??= nowUtc;

    /// <summary>Sliding expiry: a device that keeps signing in keeps its credential.</summary>
    public void Touch(DateTime nowUtc) { LastUsedAtUtc = nowUtc; ExpiresAtUtc = nowUtc.Add(Lifetime); }
}
