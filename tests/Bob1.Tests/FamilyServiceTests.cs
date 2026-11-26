using Bob1.Modules.Identity.Application;
using Bob1.Modules.Identity.Domain;
using Bob1.Modules.Identity.Infrastructure;
using Bob1.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bob1.Tests;

public class FamilyServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeClock _clock = new();
    private readonly FakeNotifications _notifications = new();
    private readonly IdentityDbContext _db = TestDb.Identity();
    private readonly List<FamilyDissolved> _dissolved = [];
    private readonly FamilyService _sut;

    public FamilyServiceTests()
    {
        var hasher = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions { IterationCount = 1_000 }));
        var tokens = new TokenService(new JwtOptions("bob1-api", "bob1-app", "unit-test-signing-key-at-least-32-characters-long!!"), _clock);
        var auth = new AuthService(_db, hasher, tokens, _clock, _notifications, NullLogger<AuthService>.Instance);
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<FamilyDissolved>>(new RecordingHandler(_dissolved));
        var events = new DomainEventDispatcher(services.BuildServiceProvider());
        _sut = new FamilyService(_db, auth, _notifications, events, _clock, NullLogger<FamilyService>.Instance);
    }

    private sealed class RecordingHandler(List<FamilyDissolved> into) : IDomainEventHandler<FamilyDissolved>
    {
        public Task HandleAsync(FamilyDissolved e, CancellationToken ct) { into.Add(e); return Task.CompletedTask; }
    }

    private async Task<User> SeedAsync(string email, UserRoles roles = UserRoles.Parent, Guid? familyId = null, bool active = true)
    {
        var user = User.Create(email, "Camille", "Martin", roles, familyId, _clock.Now);
        user.SetPasswordHash("hash");
        user.SetActive(active);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(Ct);
        return user;
    }

    private async Task<Family> SeedFamilyAsync(string name = "Martin")
    {
        var family = Family.Create(name, _clock.Now);
        _db.Families.Add(family);
        await _db.SaveChangesAsync(Ct);
        return family;
    }

    // ---- Create -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_parent_without_a_family_creates_one_named_after_their_last_name_by_default()
    {
        var user = await SeedAsync("a@bob1.local");

        var result = await _sut.CreateAsync(user.Id, "  ", Ct);

        Assert.True(result.IsSuccess);
        var family = await _db.Families.SingleAsync(Ct);
        Assert.Equal("Martin", family.Name);
        Assert.Equal(family.Id, (await _db.Users.SingleAsync(Ct)).FamilyId);
        Assert.Equal(family.Id, result.Value.User.FamilyId);            // the app learns its new family from the fresh session
        Assert.False(string.IsNullOrEmpty(result.Value.AccessToken));
    }

    [Fact]
    public async Task A_family_can_be_given_a_name()
    {
        var user = await SeedAsync("a@bob1.local");

        await _sut.CreateAsync(user.Id, "  Les Martin ", Ct);

        Assert.Equal("Les Martin", (await _db.Families.SingleAsync(Ct)).Name);
    }

    [Fact]
    public async Task Only_a_parent_can_create_a_family()
    {
        var staff = await SeedAsync("ref@bob1.local", UserRoles.Staff);

        var result = await _sut.CreateAsync(staff.Id, "Les Martin", Ct);

        Assert.Equal("family.parents_only", result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Empty(_db.Families);
    }

    [Fact]
    public async Task A_person_who_already_has_a_family_cannot_create_another()
    {
        var family = await SeedFamilyAsync();
        var user = await SeedAsync("a@bob1.local", familyId: family.Id);

        var result = await _sut.CreateAsync(user.Id, null, Ct);

        Assert.Equal("family.already_member", result.Error.Code);
        Assert.Equal(1, await _db.Families.CountAsync(Ct));
    }

    [Fact]
    public async Task A_family_name_longer_than_100_characters_is_refused()
    {
        var user = await SeedAsync("a@bob1.local");

        var result = await _sut.CreateAsync(user.Id, new string('x', 101), Ct);

        Assert.Equal("family_name.too_long", result.Error.Code);
        Assert.Empty(_db.Families);
    }

    // ---- Join ---------------------------------------------------------------------------------------------------------

    private async Task<(Family Family, User Owner)> SeedFamilyWithCodeAsync(string code = "ABCD1234", int expiresInDays = 7)
    {
        var family = await SeedFamilyAsync();
        var owner = await SeedAsync("owner@bob1.local", familyId: family.Id);
        _db.Invitations.Add(FamilyInvitation.Create(family.Id, code, owner.Id, _clock.Now.AddDays(expiresInDays)));
        await _db.SaveChangesAsync(Ct);
        return (family, owner);
    }

    [Theory]
    [InlineData(UserRoles.Parent)]
    [InlineData(UserRoles.Staff)]
    public async Task Anyone_without_a_family_joins_with_a_valid_code_and_the_members_are_told(UserRoles roles)
    {
        var (family, owner) = await SeedFamilyWithCodeAsync();
        var newcomer = await SeedAsync("new@bob1.local", roles);

        var result = await _sut.JoinAsync(newcomer.Id, " abcd1234 ", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(family.Id, (await _db.Users.SingleAsync(u => u.Id == newcomer.Id, Ct)).FamilyId);
        Assert.Equal(family.Id, result.Value.User.FamilyId);
        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal(new[] { owner.Id }, sent.Users);
        Assert.Equal("Nouveau membre dans la famille", sent.Title);
    }

    [Fact]
    public async Task A_code_works_once()
    {
        await SeedFamilyWithCodeAsync();
        var first = await SeedAsync("first@bob1.local");
        var second = await SeedAsync("second@bob1.local");

        Assert.True((await _sut.JoinAsync(first.Id, "ABCD1234", Ct)).IsSuccess);
        var again = await _sut.JoinAsync(second.Id, "ABCD1234", Ct);

        Assert.Equal("invitation.invalid", again.Error.Code);
        Assert.Null((await _db.Users.SingleAsync(u => u.Id == second.Id, Ct)).FamilyId);
    }

    [Theory]
    [InlineData("NOPE0000")]
    [InlineData("")]
    [InlineData(null)]
    public async Task An_unknown_or_empty_code_is_refused(string? code)
    {
        await SeedFamilyWithCodeAsync();
        var newcomer = await SeedAsync("new@bob1.local");

        var result = await _sut.JoinAsync(newcomer.Id, code, Ct);

        Assert.Equal("invitation.invalid", result.Error.Code);
    }

    [Fact]
    public async Task An_expired_code_is_refused()
    {
        await SeedFamilyWithCodeAsync(expiresInDays: -1);
        var newcomer = await SeedAsync("new@bob1.local");

        var result = await _sut.JoinAsync(newcomer.Id, "ABCD1234", Ct);

        Assert.Equal("invitation.invalid", result.Error.Code);
    }

    [Fact]
    public async Task A_person_who_is_already_in_a_family_must_leave_it_before_joining_another()
    {
        var (_, owner) = await SeedFamilyWithCodeAsync();
        var other = await SeedFamilyAsync("Durand");
        var member = await SeedAsync("member@bob1.local", familyId: other.Id);

        var result = await _sut.JoinAsync(member.Id, "ABCD1234", Ct);

        Assert.Equal("family.already_member", result.Error.Code);
        Assert.Equal(other.Id, (await _db.Users.SingleAsync(u => u.Id == member.Id, Ct)).FamilyId);
        Assert.NotNull(owner);
    }

    // ---- Leave --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Leaving_keeps_the_family_for_the_others_and_tells_the_active_ones()
    {
        var family = await SeedFamilyAsync();
        var leaver = await SeedAsync("leaver@bob1.local", familyId: family.Id);
        var stays = await SeedAsync("stays@bob1.local", familyId: family.Id);
        await SeedAsync("old@bob1.local", familyId: family.Id, active: false);   // still a member, but not told

        var result = await _sut.LeaveAsync(leaver.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Null((await _db.Users.SingleAsync(u => u.Id == leaver.Id, Ct)).FamilyId);
        Assert.Null(result.Value.User.FamilyId);
        Assert.Equal(family.Id, (await _db.Users.SingleAsync(u => u.Id == stays.Id, Ct)).FamilyId);
        Assert.Equal(1, await _db.Families.CountAsync(Ct));
        Assert.Empty(_dissolved);
        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal(new[] { stays.Id }, sent.Users);
        Assert.Equal("Un membre a quitté la famille", sent.Title);
    }

    [Fact]
    public async Task The_last_member_leaving_deletes_the_family_and_its_invitations()
    {
        var (family, owner) = await SeedFamilyWithCodeAsync();

        var result = await _sut.LeaveAsync(owner.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(_db.Families);
        Assert.Empty(_db.Invitations);
        Assert.Equal(family.Id, Assert.Single(_dissolved).FamilyId);   // Participation drops the presence confirmations
        Assert.Empty(_notifications.Sent);
    }

    [Fact]
    public async Task Leaving_without_a_family_is_refused()
    {
        var user = await SeedAsync("a@bob1.local");

        var result = await _sut.LeaveAsync(user.Id, Ct);

        Assert.Equal("family.none", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task A_deactivated_account_cannot_change_family()
    {
        var family = await SeedFamilyAsync();
        var user = await SeedAsync("a@bob1.local", familyId: family.Id, active: false);

        Assert.Equal(ErrorType.Unauthorized, (await _sut.LeaveAsync(user.Id, Ct)).Error.Type);
        Assert.Equal(ErrorType.Unauthorized, (await _sut.JoinAsync(user.Id, "ABCD1234", Ct)).Error.Type);
        Assert.Equal(ErrorType.Unauthorized, (await _sut.CreateAsync(user.Id, null, Ct)).Error.Type);
    }
}
