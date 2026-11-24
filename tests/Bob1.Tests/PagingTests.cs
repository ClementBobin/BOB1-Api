namespace Bob1.Tests;

public class PagingTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(30, 30)]
    public void Skip_is_never_negative(int? skip, int expected) => Assert.Equal(expected, Paging.Skip(skip));

    [Theory]
    [InlineData(null, 50)]    // default
    [InlineData(0, 1)]        // at least one row
    [InlineData(-3, 1)]
    [InlineData(20, 20)]
    [InlineData(10_000, 500)] // capped
    public void Take_is_clamped(int? take, int expected) => Assert.Equal(expected, Paging.Take(take));

    [Fact]
    public void Take_honours_custom_default_and_max()
    {
        Assert.Equal(10, Paging.Take(null, defaultTake: 10, maxTake: 20));
        Assert.Equal(20, Paging.Take(99, defaultTake: 10, maxTake: 20));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  MaRtIn ", "martin")]
    public void Term_is_trimmed_and_lower_cased(string? search, string? expected) =>
        Assert.Equal(expected, Paging.Term(search));

    [Fact]
    public void Pattern_wraps_the_term_in_wildcards()
    {
        Assert.Equal("%martin%", Paging.Pattern("  martin "));
        Assert.Null(Paging.Pattern("  "));
        Assert.Null(Paging.Pattern(null));
    }

    [Fact]
    public void Pattern_escapes_LIKE_wildcards_and_the_escape_character()
    {
        // 50%_off\  ->  %50\%\_off\\%
        Assert.Equal(@"%50\%\_off\\%", Paging.Pattern(@"50%_off\"));
    }
}
