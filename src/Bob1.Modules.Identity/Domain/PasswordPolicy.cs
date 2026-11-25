using System.Text.RegularExpressions;

namespace Bob1.Modules.Identity.Domain;

/// <summary>
/// What a new password must satisfy (used by the public password-reset link). Returns the first rule that is broken, as a stable
/// code the page translates; the length / classes / repeats / common-word rules are mirrored client-side for live feedback
/// (bob1-admin <c>lib/password-policy.ts</c>), the personal-data check is the server's alone (it knows the whole email and last name).
/// </summary>
internal static partial class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;   // bounds the hashing cost of a hostile request

    // A password containing one of these is guessed in seconds, whatever else it contains.
    private static readonly string[] CommonWords =
        ["password", "motdepasse", "azerty", "qwerty", "123456", "000000", "admin", "welcome", "bienvenue", "bob1", "basket"];

    [GeneratedRegex(@"(.)\1{3,}")]
    private static partial Regex FourInARow();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonAlphanumeric();

    public static Result Check(string? password, string email, string firstName, string lastName)
    {
        if (password is null || password.Length < MinLength)
            return Error.Validation("password.too_short", $"Password must be at least {MinLength} characters.");
        if (password.Length > MaxLength)
            return Error.Validation("password.too_long", $"Password must be at most {MaxLength} characters.");
        if (!password.Any(char.IsLower))
            return Error.Validation("password.no_lower", "Password must contain a lowercase letter.");
        if (!password.Any(char.IsUpper))
            return Error.Validation("password.no_upper", "Password must contain an uppercase letter.");
        if (!password.Any(char.IsDigit))
            return Error.Validation("password.no_digit", "Password must contain a digit.");
        if (password.All(char.IsLetterOrDigit))
            return Error.Validation("password.no_symbol", "Password must contain a special character.");
        if (FourInARow().IsMatch(password))
            return Error.Validation("password.repeated", "Password must not repeat the same character four times in a row.");
        if (CommonWords.Any(w => password.Contains(w, StringComparison.OrdinalIgnoreCase)))
            return Error.Validation("password.common", "Password contains a word that is too common.");
        if (PersonalParts(email, firstName, lastName).Any(p => password.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return Error.Validation("password.personal", "Password must not contain your name or your email address.");
        return Result.Success();
    }

    /// <summary>The pieces of who the person is that make a password guessable (3 characters or more, to avoid matching "al" in everything).</summary>
    private static IEnumerable<string> PersonalParts(string email, string firstName, string lastName)
    {
        var local = email.Split('@')[0];
        return NonAlphanumeric().Split($"{local} {firstName} {lastName}")
            .Where(p => p.Length >= 3).Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
