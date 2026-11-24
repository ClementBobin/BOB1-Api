using Bob1.Modules.Identity.Domain;

namespace Bob1.Tests;

public class IdentityDomainTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 17, 30, 0, DateTimeKind.Utc);

    // ---- User / Family ----------------------------------------------------------------------------------------------

    [Fact]
    public void A_new_user_is_active_and_not_suspended()
    {
        var familyId = Guid.NewGuid();

        var user = User.Create("camille@bob1.local", "Camille", "Martin", UserRoles.Parent, familyId, Now);

        Assert.True(user.IsActive);
        Assert.False(user.SuspendedFromParticipation);
        Assert.Equal(familyId, user.FamilyId);
        Assert.Equal(Now, user.CreatedAtUtc);
        Assert.Equal("Camille Martin", user.DisplayName);
    }

    [Fact]
    public void DisplayName_is_trimmed_when_a_part_is_missing() =>
        Assert.Equal("Ada", User.Create("ada@bob1.local", "Ada", "", UserRoles.Parent, null, Now).DisplayName);

    [Fact]
    public void User_setters_update_the_account()
    {
        var user = User.Create("a@bob1.local", "A", "B", UserRoles.Parent, null, Now);
        var familyId = Guid.NewGuid();

        user.SetName("Alex", "Admin");
        user.SetEmail("alex@bob1.local");
        user.SetFamilyId(familyId);
        user.SetRoles(UserRoles.Staff | UserRoles.Admin);
        user.SetActive(false);
        user.SetSuspendedFromParticipation(true);
        user.SetPasswordHash("hash");

        Assert.Equal("Alex Admin", user.DisplayName);
        Assert.Equal("alex@bob1.local", user.Email);
        Assert.Equal(familyId, user.FamilyId);
        Assert.Equal(UserRoles.Staff | UserRoles.Admin, user.Roles);
        Assert.False(user.IsActive);
        Assert.True(user.SuspendedFromParticipation);
        Assert.Equal("hash", user.PasswordHash);
    }

    [Fact]
    public void A_family_can_be_renamed()
    {
        var family = Family.Create("Martin", Now);

        family.Rename("Martin-Durand");

        Assert.Equal("Martin-Durand", family.Name);
    }

    // ---- Refresh token ------------------------------------------------------------------------------------------------

    [Fact]
    public void A_refresh_token_is_active_until_it_expires()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Now.AddDays(30), Now);

        Assert.True(token.IsActive(Now));
        Assert.True(token.IsActive(Now.AddDays(30).AddTicks(-1)));
        Assert.False(token.IsActive(Now.AddDays(30)));   // expiry instant itself is already expired
    }

    [Fact]
    public void Revoking_a_refresh_token_is_final_and_keeps_the_first_date()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Now.AddDays(30), Now);

        token.Revoke(Now.AddMinutes(1));
        token.Revoke(Now.AddMinutes(5));

        Assert.False(token.IsActive(Now.AddMinutes(10)));
        Assert.Equal(Now.AddMinutes(1), token.RevokedAtUtc);
    }

    // ---- Family invitation --------------------------------------------------------------------------------------------

    [Fact]
    public void An_invitation_is_usable_once_before_it_expires()
    {
        var invitation = FamilyInvitation.Create(Guid.NewGuid(), "ABCD1234", Guid.NewGuid(), Now.AddDays(7));

        Assert.True(invitation.IsUsable(Now));
        Assert.False(invitation.IsUsable(Now.AddDays(7)));

        invitation.MarkUsed(Now);

        Assert.False(invitation.IsUsable(Now));
        Assert.Equal(Now, invitation.UsedAtUtc);
    }

    // ---- Account link -------------------------------------------------------------------------------------------------

    [Fact]
    public void An_account_link_expires_after_its_lifetime()
    {
        var link = AccountLink.Create(Guid.NewGuid(), AccountLinkPurpose.PasswordReset, "hash", Guid.NewGuid(), Now, TimeSpan.FromHours(24));

        Assert.Equal(Now.AddHours(24), link.ExpiresAtUtc);
        Assert.True(link.IsUsable(Now.AddHours(23)));
        Assert.False(link.IsUsable(Now.AddHours(24)));
    }

    // ---- Biometric credential -----------------------------------------------------------------------------------------

    [Fact]
    public void A_biometric_credential_lives_90_days()
    {
        var credential = BiometricCredential.Create(Guid.NewGuid(), "hash", "Pixel 8", Now);

        Assert.Equal(TimeSpan.FromDays(90), BiometricCredential.Lifetime);
        Assert.Equal(Now.AddDays(90), credential.ExpiresAtUtc);
        Assert.Equal("Pixel 8", credential.DeviceName);
        Assert.True(credential.IsActive(Now));
        Assert.False(credential.IsActive(Now.AddDays(90)));
    }

    [Fact]
    public void Touching_a_credential_slides_its_expiry()
    {
        var credential = BiometricCredential.Create(Guid.NewGuid(), "hash", null, Now);
        var later = Now.AddDays(60);

        credential.Touch(later);

        Assert.Equal(later, credential.LastUsedAtUtc);
        Assert.Equal(later.AddDays(90), credential.ExpiresAtUtc);
        Assert.True(credential.IsActive(Now.AddDays(100)));   // would have expired without the touch
    }

    [Fact]
    public void A_revoked_credential_is_never_active_again()
    {
        var credential = BiometricCredential.Create(Guid.NewGuid(), "hash", null, Now);

        credential.Revoke(Now.AddDays(1));

        Assert.False(credential.IsActive(Now.AddDays(2)));
        Assert.Equal(Now.AddDays(1), credential.RevokedAtUtc);
    }

    [Fact]
    public void A_device_can_have_five_credentials_at_most() => Assert.Equal(5, BiometricCredential.MaxPerUser);
}
