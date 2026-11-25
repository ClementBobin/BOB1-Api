using System.Text.Json;
using Bob1.Modules.Identity.Application;
using Bob1.Modules.Identity.Domain;
using Bob1.Modules.Identity.Infrastructure;
using Bob1.Shared.Contracts.Privacy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bob1.Tests;

public class PersonalDataServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeClock _clock = new();
    private readonly IdentityDbContext _db = TestDb.Identity();
    private readonly FakeContributor _participation = new("participation", new { slotSignups = new[] { "Buvette" } });
    private readonly FakeContributor _carpool = new("carpool", new { ridesOffered = Array.Empty<string>() });
    private readonly PersonalDataService _sut;

    public PersonalDataServiceTests()
    {
        var events = new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider());
        _sut = new PersonalDataService(_db, [_participation, _carpool], events, _clock, NullLogger<PersonalDataService>.Instance);
    }

    private sealed class FakeContributor(string section, object data) : IPersonalDataContributor
    {
        public List<PersonalDataSubject> Exported { get; } = [];
        public List<PersonalDataSubject> Erased { get; } = [];
        public string Section => section;
        public Task<object> ExportAsync(PersonalDataSubject s, CancellationToken ct) { Exported.Add(s); return Task.FromResult(data); }
        public Task EraseAsync(PersonalDataSubject s, CancellationToken ct) { Erased.Add(s); return Task.CompletedTask; }
    }

    private async Task<User> SeedAsync(string email, UserRoles roles = UserRoles.Parent, Guid? familyId = null, bool active = true)
    {
        var user = User.Create(email, "Camille", "Martin", roles, familyId, _clock.Now);
        user.SetPasswordHash("hash-that-must-never-leak");
        user.SetActive(active);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(Ct);
        return user;
    }

    private async Task<Family> SeedFamilyAsync()
    {
        var family = Family.Create("Martin", _clock.Now);
        _db.Families.Add(family);
        await _db.SaveChangesAsync(Ct);
        return family;
    }

    [Fact]
    public async Task Export_holds_the_account_the_family_and_every_module_section_but_never_the_password_hash()
    {
        var family = await SeedFamilyAsync();
        var user = await SeedAsync("camille@bob1.local", familyId: family.Id);
        await SeedAsync("julie@bob1.local", familyId: family.Id);

        var export = await _sut.ExportAsync(user, Ct);
        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("camille@bob1.local", root.GetProperty("account").GetProperty("email").GetString());
        Assert.Equal("Martin", root.GetProperty("family").GetProperty("name").GetString());
        Assert.Equal(2, root.GetProperty("family").GetProperty("members").GetInt32());   // a count: the other member's identity is not ours to hand over
        Assert.True(root.GetProperty("data").TryGetProperty("participation", out _));
        Assert.True(root.GetProperty("data").TryGetProperty("carpool", out _));
        Assert.DoesNotContain("hash-that-must-never-leak", json);
        Assert.DoesNotContain("julie@bob1.local", json);
    }

    [Fact]
    public async Task Export_tells_the_modules_whether_the_person_is_the_last_of_their_family()
    {
        var shared = await SeedFamilyAsync();
        var first = await SeedAsync("a@bob1.local", familyId: shared.Id);
        await SeedAsync("b@bob1.local", familyId: shared.Id);
        var soloFamily = await SeedFamilyAsync();
        var solo = await SeedAsync("c@bob1.local", familyId: soloFamily.Id);
        var noFamily = await SeedAsync("d@bob1.local");

        await _sut.ExportAsync(first, Ct);
        await _sut.ExportAsync(solo, Ct);
        await _sut.ExportAsync(noFamily, Ct);

        Assert.Collection(_participation.Exported,
            s => Assert.False(s.FamilyEmptied),
            s => Assert.True(s.FamilyEmptied),
            s => { Assert.False(s.FamilyEmptied); Assert.Null(s.FamilyId); });
    }

    [Fact]
    public async Task Erase_refuses_the_last_active_admin_and_touches_nothing()
    {
        var admin = await SeedAsync("admin@bob1.local", UserRoles.Admin | UserRoles.Staff);
        await SeedAsync("parent@bob1.local");                                  // not an admin
        await SeedAsync("old-admin@bob1.local", UserRoles.Admin, active: false);   // an admin who cannot sign in does not count

        var result = await _sut.EraseAsync(admin, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("user.last_admin", result.Error.Code);
        Assert.True((await _db.Users.SingleAsync(u => u.Id == admin.Id, Ct)).IsActive);
        Assert.Empty(_participation.Erased);
        Assert.Empty(_carpool.Erased);
    }
}
