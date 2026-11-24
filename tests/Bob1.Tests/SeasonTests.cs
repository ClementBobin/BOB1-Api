namespace Bob1.Tests;

public class SeasonTests
{
    [Theory]
    [InlineData(2026, 9, 1, "2026-2027")]    // first day of the season
    [InlineData(2026, 12, 31, "2026-2027")]
    [InlineData(2027, 1, 1, "2026-2027")]
    [InlineData(2027, 8, 31, "2026-2027")]   // last day of the season
    [InlineData(2026, 8, 31, "2025-2026")]
    [InlineData(2026, 6, 15, "2025-2026")]
    public void For_derives_the_label_from_the_date(int year, int month, int day, string expected) =>
        Assert.Equal(expected, Season.For(new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void Current_uses_the_clock()
    {
        var clock = new FakeClock(new DateTime(2027, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal("2027-2028", Season.Current(clock));

        clock.Advance(TimeSpan.FromSeconds(-1));
        Assert.Equal("2026-2027", Season.Current(clock));
    }

    [Theory]
    [InlineData("2026-2027", true)]
    [InlineData("1999-2000", true)]
    [InlineData("2026-2028", false)]   // not consecutive
    [InlineData("2027-2026", false)]
    [InlineData("2026/2027", false)]
    [InlineData("26-27", false)]
    [InlineData("abcd-efgh", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_accepts_consecutive_years_only(string? label, bool expected) =>
        Assert.Equal(expected, Season.IsValid(label));

    [Fact]
    public void Season_boundaries_are_1_september_and_31_august()
    {
        Assert.Equal(new DateOnly(2026, 9, 1), Season.StartsOn("2026-2027"));
        Assert.Equal(new DateOnly(2027, 8, 31), Season.EndsOn("2026-2027"));
    }

    [Fact]
    public void Previous_and_Label_build_labels()
    {
        Assert.Equal("2025-2026", Season.Previous("2026-2027"));
        Assert.Equal("2030-2031", Season.Label(2030));
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("2026-2028")]
    public void Invalid_labels_throw_when_a_date_is_needed(string label)
    {
        Assert.Throws<ArgumentException>(() => Season.StartsOn(label));
        Assert.Throws<ArgumentException>(() => Season.EndsOn(label));
        Assert.Throws<ArgumentException>(() => Season.Previous(label));
    }
}
