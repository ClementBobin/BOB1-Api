using Bob1.Modules.Identity.Domain;

namespace Bob1.Tests;

public class UserRolesTests
{
    [Fact]
    public void ToNames_lists_each_role_flag()
    {
        Assert.Equal(new[] { "Parent" }, UserRoles.Parent.ToNames());
        Assert.Equal(new[] { "Staff", "Admin" }, (UserRoles.Admin | UserRoles.Staff).ToNames());
        Assert.Equal(new[] { "Parent", "Staff", "Admin" }, (UserRoles.Parent | UserRoles.Staff | UserRoles.Admin).ToNames());
    }

    [Fact]
    public void ToNames_is_empty_without_roles() => Assert.Empty(UserRoles.None.ToNames());

    [Fact]
    public void TryParse_combines_names_ignoring_case()
    {
        var ok = UserRolesExtensions.TryParse(["parent", "STAFF"], out var roles);

        Assert.True(ok);
        Assert.Equal(UserRoles.Parent | UserRoles.Staff, roles);
    }

    [Theory]
    [InlineData("superuser")]
    [InlineData("None")]   // None is not a role that can be granted
    [InlineData("")]
    public void TryParse_rejects_unknown_names(string name)
    {
        var ok = UserRolesExtensions.TryParse(["Parent", name], out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParse_of_nothing_gives_no_roles()
    {
        var ok = UserRolesExtensions.TryParse([], out var roles);

        Assert.True(ok);
        Assert.Equal(UserRoles.None, roles);
    }
}
