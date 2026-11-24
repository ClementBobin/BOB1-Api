using Bob1.Modules.Identity.Domain;
using Bob1.Modules.Identity.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Bob1.Tests;

public class TokenServiceTests
{
    private const string Key = "unit-test-signing-key-at-least-32-characters-long!!";
    private static readonly JwtOptions Jwt = new("bob1-api", "bob1-app", Key);

    private readonly FakeClock _clock = new();

    private TokenService NewService() => new(Jwt, _clock);

    private static User NewUser(UserRoles roles = UserRoles.Parent, Guid? familyId = null) =>
        User.Create("camille@bob1.local", "Camille", "Martin", roles, familyId, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Hash_is_the_uppercase_hex_sha256()
    {
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", TokenService.Hash("abc"));
    }

    [Fact]
    public void Hash_is_deterministic_and_64_chars()
    {
        Assert.Equal(TokenService.Hash("same"), TokenService.Hash("same"));
        Assert.NotEqual(TokenService.Hash("one"), TokenService.Hash("two"));
        Assert.Equal(64, TokenService.Hash("anything").Length);
    }

    [Fact]
    public void Opaque_tokens_are_random_url_safe_and_hashed_for_storage()
    {
        var service = NewService();

        var (token, hash) = service.NewOpaqueToken();
        var (other, _) = service.NewOpaqueToken();

        Assert.NotEqual(token, other);
        Assert.Equal(43, token.Length);   // 32 random bytes in unpadded base64
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
        Assert.Equal(TokenService.Hash(token), hash);
        Assert.NotEqual(token, hash);     // the raw token is never what gets stored
    }

    [Fact]
    public void Refresh_tokens_use_the_same_format()
    {
        var (token, hash) = NewService().NewRefreshToken();

        Assert.Equal(43, token.Length);
        Assert.Equal(TokenService.Hash(token), hash);
    }

    [Fact]
    public void Lifetimes_are_30_minutes_and_30_days()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), TokenService.AccessLifetime);
        Assert.Equal(TimeSpan.FromDays(30), TokenService.RefreshLifetime);
    }

    [Fact]
    public void The_access_token_carries_the_identity_claims()
    {
        var familyId = Guid.NewGuid();
        var user = NewUser(UserRoles.Staff | UserRoles.Admin, familyId);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(NewService().CreateAccessToken(user));

        Assert.Equal(user.Id.ToString(), jwt.GetClaim("sub").Value);
        Assert.Equal(user.Id.ToString(), jwt.GetClaim("person_id").Value);
        Assert.Equal("camille@bob1.local", jwt.GetClaim("email").Value);
        Assert.Equal("Camille Martin", jwt.GetClaim("name").Value);
        Assert.Equal(familyId.ToString(), jwt.GetClaim("family_id").Value);
        Assert.Equal(new[] { "Admin", "Staff" }, jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).OrderBy(v => v).ToArray());
        Assert.Equal("bob1-api", jwt.Issuer);
        Assert.Contains("bob1-app", jwt.Audiences);
    }

    [Fact]
    public void A_user_without_a_family_gets_no_family_claim()
    {
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(NewService().CreateAccessToken(NewUser(familyId: null)));

        Assert.False(jwt.TryGetClaim("family_id", out _));
    }

    [Fact]
    public void The_access_token_expires_30_minutes_after_the_clock()
    {
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(NewService().CreateAccessToken(NewUser()));

        Assert.Equal(_clock.Now.AddMinutes(30), jwt.ValidTo);
    }

    [Fact]
    public async Task The_access_token_is_signed_with_the_configured_key()
    {
        var token = NewService().CreateAccessToken(NewUser());
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = "bob1-api",
            ValidAudience = "bob1-app",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            ValidateLifetime = false,
        };

        var valid = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        Assert.True(valid.IsValid);

        parameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-key-another-key-another-key-123456"));
        var forged = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        Assert.False(forged.IsValid);
    }
}
