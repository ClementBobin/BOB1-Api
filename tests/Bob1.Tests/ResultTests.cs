namespace Bob1.Tests;

public class ResultTests
{
    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_exposes_the_error()
    {
        var error = Error.Conflict("slot.full", "This slot is full.");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void An_error_converts_implicitly_to_a_failed_result()
    {
        Result result = Error.NotFound("x.not_found", "nope");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public void A_value_converts_implicitly_to_a_successful_typed_result()
    {
        Result<string> result = "ok";

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public void A_failed_typed_result_has_no_value()
    {
        Result<string> result = Error.Validation("x.invalid", "bad");

        Assert.False(result.IsSuccess);
        Assert.Equal("x.invalid", result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_factories_set_the_matching_type()
    {
        Assert.Equal(ErrorType.Validation, Error.Validation("c", "d").Type);
        Assert.Equal(ErrorType.NotFound, Error.NotFound("c", "d").Type);
        Assert.Equal(ErrorType.Conflict, Error.Conflict("c", "d").Type);
        Assert.Equal(ErrorType.Forbidden, Error.Forbidden("c", "d").Type);
        Assert.Equal(ErrorType.Unauthorized, Error.Unauthorized("c", "d").Type);
    }
}
