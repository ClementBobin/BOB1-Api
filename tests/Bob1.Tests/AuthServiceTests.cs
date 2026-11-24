using Bob1.Modules.Identity.Application;
using Bob1.Modules.Identity.Domain;
using Bob1.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bob1.Tests;

public class AuthServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeClock _clock = new();
    private readonly FakeNotifications _notifications = new();
    private readonly IdentityDbContext _db = TestDb.Identity();
    private readonly IPasswordHasher<User> _hasher =
        new PasswordHasher<User>(Options.Create(new PasswordHasherOptions { IterationCount = 1_000 }));   // fast hashing for tests
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var tokens = new TokenService(new JwtOptions("bob1-api", "bob1-app", "unit-test-signing-key-at-least-32-characters-long!!"), _clock);
        _sut = new AuthService(_db, _hasher, tokens, _clock, _notifications, NullLogger<AuthService>.Instance);
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------

    private async Task<User> SeedUserAsync(string email = "parent@bob1.local", string password = "Parent123!",
        UserRoles roles = UserRoles.Parent, bool active = true, Guid? familyId = null)
    {
        var user = User.Create(email, "Camille", "Martin", roles, familyId, _clock.Now);
        user.SetPasswordHash(_hasher.HashPassword(user, password));
        user.SetActive(active);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(Ct);
        return user;
    }

    private static RegisterRequest Register(string email = "camille@bob1.local", string password = "Password1",
        string first = "Camille", string last = "Martin", string? familyName = null, string? code = null) =>
        new(email, password, first, last, familyName, code);

    private async Task<AuthResponse> LoginOkAsync(string email = "parent@bob1.local", string password = "Parent123!")
    {
        var result = await _sut.LoginAsync(new LoginRequest(email, password), Ct);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    // ---- Register -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Register_creates_a_parent_in_a_new_family_and_signs_in()
    {
        var result = await _sut.RegisterAsync(Register(email: "  Camille@Bob1.Local "), Ct);

        Assert.True(result.IsSuccess);
        var user = await _db.Users.SingleAsync(Ct);
        Assert.Equal("camille@bob1.local", user.Email);   // trimmed and lower-cased
        Assert.Equal(UserRoles.Parent, user.Roles);
        Assert.NotEqual("Password1", user.PasswordHash);
        var family = await _db.Families.SingleAsync(Ct);
        Assert.Equal(family.Id, user.FamilyId);
        Assert.Equal("Martin", family.Name);              // defaults to the last name
        Assert.False(string.IsNullOrEmpty(result.Value.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));
        Assert.Equal(1800, result.Value.ExpiresInSeconds);
        Assert.Equal(new[] { "Parent" }, result.Value.User.Roles);
    }

    [Fact]
    public async Task Register_uses_the_given_family_name()
    {
        await _sut.RegisterAsync(Register(familyName: "  Les Martin "), Ct);

        Assert.Equal("Les Martin", (await _db.Families.SingleAsync(Ct)).Name);
    }

    [Theory]
    [InlineData("", "Password1", "Camille", "Martin", "email.required")]
    [InlineData("camille@bob1.local", "Password1", "", "Martin", "first_name.required")]
    [InlineData("camille@bob1.local", "Password1", "Camille", "  ", "last_name.required")]
    [InlineData("not-an-email", "Password1", "Camille", "Martin", "email.invalid")]
    [InlineData("camille@bob1.local", "short", "Camille", "Martin", "password.too_short")]
    public async Task Register_validates_the_input(string email, string password, string first, string last, string code)
    {
        var result = await _sut.RegisterAsync(Register(email, password, first, last), Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(_db.Users);
    }

    [Fact]
    public async Task Register_refuses_an_email_that_is_already_taken_whatever_its_case()
    {
        await SeedUserAsync("taken@bob1.local");

        var result = await _sut.RegisterAsync(Register(email: "TAKEN@bob1.local"), Ct);

        Assert.Equal("email.taken", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(1, await _db.Users.CountAsync(Ct));
    }

    [Fact]
    public async Task Register_with_an_invitation_joins_the_family_and_notifies_its_members()
    {
        var family = Family.Create("Martin", _clock.Now);
        _db.Families.Add(family);
        var owner = await SeedUserAsync("owner@bob1.local", familyId: family.Id);
        var invitation = FamilyInvitation.Create(family.Id, "ABCD1234", owner.Id, _clock.Now.AddDays(7));
        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync(Ct);

        var result = await _sut.RegisterAsync(Register(email: "partner@bob1.local", code: " abcd1234 "), Ct);

        Assert.True(result.IsSuccess);
        var partner = await _db.Users.SingleAsync(u => u.Email == "partner@bob1.local", Ct);
        Assert.Equal(family.Id, partner.FamilyId);
        Assert.Equal(1, await _db.Families.CountAsync(Ct));   // no extra family
        Assert.False(invitation.IsUsable(_clock.Now));         // single use
        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal(new[] { owner.Id }, sent.Users);          // the newcomer is not notified about themselves
    }

    [Fact]
    public async Task Register_refuses_unknown_expired_and_used_invitations()
    {
        var family = Family.Create("Martin", _clock.Now);
        _db.Families.Add(family);
        var owner = await SeedUserAsync("owner@bob1.local", familyId: family.Id);
        _db.Invitations.Add(FamilyInvitation.Create(family.Id, "EXPIRED1", owner.Id, _clock.Now.AddDays(-1)));
        var used = FamilyInvitation.Create(family.Id, "USED0001", owner.Id, _clock.Now.AddDays(7));
        used.MarkUsed(_clock.Now);
        _db.Invitations.Add(used);
        await _db.SaveChangesAsync(Ct);

        foreach (var code in new[] { "NOPE0000", "EXPIRED1", "USED0001" })
        {
            var result = await _sut.RegisterAsync(Register(email: $"{code.ToLowerInvariant()}@bob1.local", code: code), Ct);

            Assert.Equal("invitation.invalid", result.Error.Code);
        }

        Assert.Equal(1, await _db.Users.CountAsync(Ct));       // only the owner
        Assert.Empty(_notifications.Sent);
    }

    // ---- Login --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Login_issues_tokens_and_stores_only_the_refresh_token_hash()
    {
        await SeedUserAsync();

        var auth = await LoginOkAsync();

        Assert.Equal("parent@bob1.local", auth.User.Email);
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        var stored = await _db.RefreshTokens.SingleAsync(Ct);
        Assert.Equal(TokenService.Hash(auth.RefreshToken), stored.TokenHash);
        Assert.NotEqual(auth.RefreshToken, stored.TokenHash);
        Assert.Equal(_clock.Now.Add(TokenService.RefreshLifetime), stored.ExpiresAtUtc);
    }

    [Fact]
    public async Task Login_ignores_email_case_and_surrounding_spaces()
    {
        await SeedUserAsync("parent@bob1.local");

        var result = await _sut.LoginAsync(new LoginRequest("  PARENT@bob1.local ", "Parent123!"), Ct);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("parent@bob1.local", "wrong-password")]
    [InlineData("parent@bob1.local", "")]
    [InlineData("nobody@bob1.local", "Parent123!")]
    public async Task Login_fails_the_same_way_for_bad_credentials(string email, string password)
    {
        await SeedUserAsync();

        var result = await _sut.LoginAsync(new LoginRequest(email, password), Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("auth.invalid_credentials", result.Error.Code);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Empty(_db.RefreshTokens);
    }

    [Fact]
    public async Task Login_refuses_a_deactivated_account()
    {
        await SeedUserAsync(active: false);

        var result = await _sut.LoginAsync(new LoginRequest("parent@bob1.local", "Parent123!"), Ct);

        Assert.Equal("auth.invalid_credentials", result.Error.Code);
    }

    // ---- Refresh / logout ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_rotates_the_token_and_revokes_the_old_one()
    {
        await SeedUserAsync();
        var first = await LoginOkAsync();

        var refreshed = await _sut.RefreshAsync(new RefreshRequest(first.RefreshToken), Ct);

        Assert.True(refreshed.IsSuccess);
        Assert.NotEqual(first.RefreshToken, refreshed.Value.RefreshToken);
        var oldRow = await _db.RefreshTokens.SingleAsync(t => t.TokenHash == TokenService.Hash(first.RefreshToken), Ct);
        Assert.NotNull(oldRow.RevokedAtUtc);

        // The old token cannot be replayed.
        var replay = await _sut.RefreshAsync(new RefreshRequest(first.RefreshToken), Ct);
        Assert.Equal("auth.invalid_refresh_token", replay.Error.Code);
    }

    [Fact]
    public async Task Refresh_refuses_an_expired_token()
    {
        await SeedUserAsync();
        var auth = await LoginOkAsync();

        _clock.Advance(TokenService.RefreshLifetime + TimeSpan.FromMinutes(1));
        var result = await _sut.RefreshAsync(new RefreshRequest(auth.RefreshToken), Ct);

        Assert.Equal("auth.invalid_refresh_token", result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("never-issued")]
    public async Task Refresh_refuses_blank_and_unknown_tokens(string token)
    {
        var result = await _sut.RefreshAsync(new RefreshRequest(token), Ct);

        Assert.Equal("auth.invalid_refresh_token", result.Error.Code);
    }

    [Fact]
    public async Task Refresh_refuses_a_user_who_was_deactivated_since()
    {
        var user = await SeedUserAsync();
        var auth = await LoginOkAsync();
        user.SetActive(false);
        await _db.SaveChangesAsync(Ct);

        var result = await _sut.RefreshAsync(new RefreshRequest(auth.RefreshToken), Ct);

        Assert.Equal("auth.invalid_refresh_token", result.Error.Code);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        await SeedUserAsync();
        var auth = await LoginOkAsync();

        await _sut.LogoutAsync(auth.RefreshToken, Ct);

        var result = await _sut.RefreshAsync(new RefreshRequest(auth.RefreshToken), Ct);
        Assert.Equal("auth.invalid_refresh_token", result.Error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("never-issued")]
    public async Task Logout_with_nothing_to_revoke_does_not_fail(string? token) =>
        await _sut.LogoutAsync(token, Ct);

    // ---- Biometric sign-in --------------------------------------------------------------------------------------------

    [Fact]
    public async Task Enroll_returns_the_only_copy_of_the_token_and_stores_its_hash()
    {
        var user = await SeedUserAsync();

        var result = await _sut.EnrollBiometricAsync(user.Id, new BiometricEnrollRequest("  Pixel 8 "), Ct);

        Assert.True(result.IsSuccess);
        var credential = await _db.BiometricCredentials.SingleAsync(Ct);
        Assert.Equal(result.Value.CredentialId, credential.Id);
        Assert.Equal(TokenService.Hash(result.Value.BiometricToken), credential.TokenHash);
        Assert.Equal("Pixel 8", credential.DeviceName);
    }

    [Fact]
    public async Task Enroll_truncates_a_long_device_name_and_drops_a_blank_one()
    {
        var user = await SeedUserAsync();

        await _sut.EnrollBiometricAsync(user.Id, new BiometricEnrollRequest(new string('x', 150)), Ct);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _sut.EnrollBiometricAsync(user.Id, new BiometricEnrollRequest("   "), Ct);

        var credentials = await _db.BiometricCredentials.OrderBy(c => c.CreatedAtUtc).ToListAsync(Ct);
        Assert.Equal(100, credentials[0].DeviceName!.Length);
        Assert.Null(credentials[1].DeviceName);
    }

    [Fact]
    public async Task Enroll_refuses_an_unknown_or_inactive_user()
    {
        var inactive = await SeedUserAsync(active: false);

        var unknown = await _sut.EnrollBiometricAsync(Guid.NewGuid(), new BiometricEnrollRequest(null), Ct);
        var disabled = await _sut.EnrollBiometricAsync(inactive.Id, new BiometricEnrollRequest(null), Ct);

        Assert.Equal(ErrorType.Unauthorized, unknown.Error.Type);
        Assert.Equal(ErrorType.Unauthorized, disabled.Error.Type);
        Assert.Empty(_db.BiometricCredentials);
    }

    [Fact]
    public async Task Enrolling_a_sixth_device_revokes_the_oldest()
    {
        var user = await SeedUserAsync();
        var tokens = new List<string>();
        for (var i = 0; i < BiometricCredential.MaxPerUser + 1; i++)
        {
            var enrolled = await _sut.EnrollBiometricAsync(user.Id, new BiometricEnrollRequest($"Phone {i}"), Ct);
            tokens.Add(enrolled.Value.BiometricToken);
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        var active = await _db.BiometricCredentials.Where(c => c.RevokedAtUtc == null).ToListAsync(Ct);
        Assert.Equal(BiometricCredential.MaxPerUser, active.Count);
        Assert.DoesNotContain(active, c => c.DeviceName == "Phone 0");

        var oldest = await _sut.LoginBiometricAsync(new BiometricLoginRequest(tokens[0]), Ct);
        Assert.Equal("auth.invalid_biometric_token", oldest.Error.Code);
        var newest = await _sut.LoginBiometricAsync(new BiometricLoginRequest(tokens[^1]), Ct);
        Assert.True(newest.IsSuccess);
    }

    [Fact]
    public async Task Biometric_login_gives_a_normal_session_and_slides_the_expiry()
    {
        var user = await SeedUserAsync();
        var enrolled = (await _sut.EnrollBiometricAsync(user.Id, new BiometricEnrollRequest("Pixel 8"), Ct)).Value;
        _clock.Advance(TimeSpan.FromDays(10));

        var result = await _sut.LoginBiometricAsync(new BiometricLoginRequest(enrolled.BiometricToken), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.False(string.IsNullOrEmpty(result.Value.AccessToken));
        var credential = await _db.BiometricCredentials.SingleAsync(Ct);
        Assert.Equal(_clock.Now, credential.LastUsedAtUtc);
        Assert.Equal(_clock.Now.Add(BiometricCredential.Lifetime), credential.ExpiresAtUtc);
        Assert.Single(_db.RefreshTokens);
    }

    [Theory]
    [InlineData("")]
    [InlineData("never-enrolled")]
    public async Task Biometric_login_refuses_blank_and_unknown_tokens(string token)
    {
        var result = await _sut.LoginBiometricAsync(new BiometricLoginRequest(token), Ct);

        Assert.Equal("auth.invalid_biometric_token", result.Error.Code);
    }

    [Fact]
    public async Task Biometric_login_refuses_an_expired_credential()
    {
        var user = await SeedUserAsync();
        var enrolled = (await _sut.EnrollBiometricAsync(user.Id, new BiometricEnrollRequest(null), Ct)).Value;

        _clock.Advance(BiometricCredential.Lifetime + TimeSpan.FromMinutes(1));
        var result = await _sut.LoginBiometricAsync(new BiometricLoginRequest(enrolled.BiometricToken), Ct);

        Assert.Equal("auth.invalid_biometric_token", result.Error.Code);
    }

    [Fact]
    public async Task Biometric_login_refuses_a_deactivated_user()
    {
        var user = await SeedUserAsync();
        var enrolled = (await _sut.EnrollBiometricAsync(user.Id, new BiometricEnrollRequest(null), Ct)).Value;
        user.SetActive(false);
        await _db.SaveChangesAsync(Ct);

        var result = await _sut.LoginBiometricAsync(new BiometricLoginRequest(enrolled.BiometricToken), Ct);

        Assert.Equal("auth.invalid_biometric_token", result.Error.Code);
    }

    [Fact]
    public async Task Disable_revokes_only_the_given_credential()
    {
        var user = await SeedUserAsync();
        var first = (await _sut.EnrollBiometricAsync(user.Id, new BiometricEnrollRequest("A"), Ct)).Value;
        _clock.Advance(TimeSpan.FromMinutes(1));
        var second = (await _sut.EnrollBiometricAsync(user.Id, new BiometricEnrollRequest("B"), Ct)).Value;

        await _sut.DisableBiometricAsync(user.Id, first.CredentialId, Ct);

        Assert.False((await _sut.LoginBiometricAsync(new BiometricLoginRequest(first.BiometricToken), Ct)).IsSuccess);
        Assert.True((await _sut.LoginBiometricAsync(new BiometricLoginRequest(second.BiometricToken), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Disable_without_an_id_revokes_every_credential_of_the_user_only()
    {
        var user = await SeedUserAsync();
        var other = await SeedUserAsync("other@bob1.local");
        var mine = (await _sut.EnrollBiometricAsync(user.Id, new BiometricEnrollRequest("A"), Ct)).Value;
        var theirs = (await _sut.EnrollBiometricAsync(other.Id, new BiometricEnrollRequest("B"), Ct)).Value;

        await _sut.DisableBiometricAsync(user.Id, null, Ct);

        Assert.False((await _sut.LoginBiometricAsync(new BiometricLoginRequest(mine.BiometricToken), Ct)).IsSuccess);
        Assert.True((await _sut.LoginBiometricAsync(new BiometricLoginRequest(theirs.BiometricToken), Ct)).IsSuccess);
    }
}
