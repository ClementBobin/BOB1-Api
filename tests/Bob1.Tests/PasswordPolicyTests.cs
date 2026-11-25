using Bob1.Modules.Identity.Domain;

namespace Bob1.Tests;

public class PasswordPolicyTests
{
    private static Result Check(string? password) =>
        PasswordPolicy.Check(password, "camille.martin@bob1.local", "Camille", "Martin");

    [Theory]
    [InlineData("Corr3ct-Horse-Battery!")]
    [InlineData("Exactly12ch!")]                                      // the minimum length
    [InlineData("une phrase de passe 7 mots, avec Espaces")]          // spaces count as special characters
    public void A_strong_password_is_accepted(string password) => Assert.True(Check(password).IsSuccess);

    [Theory]
    [InlineData(null, "password.too_short")]
    [InlineData("", "password.too_short")]
    [InlineData("Aa1!aaaaaaa", "password.too_short")]                 // 11 characters
    [InlineData("ABCDEFGH-12345!", "password.no_lower")]
    [InlineData("abcdefgh-12345!", "password.no_upper")]
    [InlineData("Abcdefgh-ijklmn!", "password.no_digit")]
    [InlineData("Abcdefgh1jklmnop", "password.no_symbol")]
    [InlineData("Zebra-Ahhhh-5-yes!", "password.repeated")]
    [InlineData("Super-Basket-2026!", "password.common")]
    [InlineData("MyPassword-9-Zx!", "password.common")]
    [InlineData("Camille-Rocks-77!", "password.personal")]            // first name
    [InlineData("Martin#Strong#99x", "password.personal")]            // last name
    [InlineData("Xx.CAMILLE.martin.9!", "password.personal")]         // email local part, any case
    public void A_weak_password_is_refused_with_the_first_broken_rule(string? password, string code)
    {
        var result = Check(password);

        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Error.Code);
    }

    [Fact]
    public void A_password_longer_than_the_maximum_is_refused()
    {
        var result = Check("Aa1!" + new string('x', PasswordPolicy.MaxLength - 3));   // MaxLength + 1 characters

        Assert.Equal("password.too_long", result.Error.Code);
    }

    [Fact]
    public void Short_pieces_of_a_name_do_not_forbid_everything()
    {
        // "Al" (2 characters) is not a forbidden fragment, otherwise nearly every password would match.
        Assert.True(PasswordPolicy.Check("Corr3ct-Horse-Battery!", "al@bob1.local", "Al", "Li").IsSuccess);
    }
}
