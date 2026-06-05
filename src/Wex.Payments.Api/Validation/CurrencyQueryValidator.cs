using System.Diagnostics.CodeAnalysis;

namespace Wex.Payments.Api.Validation;

/// <summary>
/// Validates the user-supplied <c>currency</c> query parameter before it is used to build the
/// Treasury rates-of-exchange filter. The filter DSL uses ':' and ',' as clause delimiters, so
/// permitting them would let a caller inject or alter filter clauses; those characters (and
/// control characters) are rejected and the length is capped, producing a clean 400 at the edge
/// rather than a distorted upstream query or a confusing 502.
/// </summary>
public static class CurrencyQueryValidator
{
    // Treasury country_currency_desc values are short (well under 60); 100 is a safe cap.
    public const int MaxLength = 100;

    /// <summary>
    /// Returns <c>true</c> when <paramref name="currency"/> is acceptable (it is then guaranteed
    /// non-null and non-whitespace); otherwise returns <c>false</c> and sets
    /// <paramref name="error"/> to a human-readable message. Every real currency name — letters
    /// (including accents), spaces, '-', '&amp;', '.', apostrophes — is accepted; only the filter
    /// delimiters and control characters are rejected.
    /// </summary>
    public static bool TryValidate(
        [NotNullWhen(true)] string? currency,
        [NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            error = "The 'currency' query parameter is required (e.g. 'Canada-Dollar').";
            return false;
        }

        var trimmed = currency.Trim();

        if (trimmed.Length > MaxLength)
        {
            error = $"currency must not exceed {MaxLength} characters.";
            return false;
        }

        foreach (var c in trimmed)
        {
            if (c is ':' or ',' || char.IsControl(c))
            {
                error = "currency contains invalid characters.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
