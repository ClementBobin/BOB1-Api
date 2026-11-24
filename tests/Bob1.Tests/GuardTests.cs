namespace Bob1.Tests;

public class GuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotBlank_rejects_missing_values(string? value)
    {
        var error = Guard.NotBlank(value, "email");

        Assert.NotNull(error);
        Assert.Equal("email.required", error.Code);
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void NotBlank_rejects_values_over_the_limit()
    {
        var error = Guard.NotBlank(new string('a', 11), "name", max: 10);

        Assert.NotNull(error);
        Assert.Equal("name.too_long", error.Code);
    }

    [Fact]
    public void NotBlank_measures_the_trimmed_value()
    {
        Assert.Null(Guard.NotBlank("  abcde  ", "name", max: 5));
    }

    [Fact]
    public void NotBlank_accepts_a_valid_value() => Assert.Null(Guard.NotBlank("Camille", "first_name"));

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(0, false)]
    [InlineData(11, false)]
    public void InRange_is_inclusive(int value, bool valid)
    {
        var error = Guard.InRange(value, "seats", 1, 10);

        Assert.Equal(valid, error is null);
        if (!valid) Assert.Equal("seats.out_of_range", error!.Code);
    }

    [Fact]
    public void First_returns_the_first_error_and_ignores_the_rest()
    {
        var first = Error.Validation("a", "first");
        var second = Error.Validation("b", "second");

        Assert.Same(first, Guard.First(null, first, second));
    }

    [Fact]
    public void First_returns_null_when_everything_is_valid() => Assert.Null(Guard.First(null, null));
}
