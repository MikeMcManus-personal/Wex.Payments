using Wex.Payments.Api.Validation;

namespace Wex.Payments.UnitTests.Validation;

[TestFixture]
public sealed class CurrencyQueryValidatorTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void TryValidate_Fails_WithRequiredMessage_WhenMissing(string? currency)
    {
        var ok = CurrencyQueryValidator.TryValidate(currency, out var error);

        Assert.That(ok, Is.False);
        Assert.That(error, Does.Contain("required"));
    }

    [TestCase("Brazil-Real,record_date:gte:2000-01-01")] // injected filter clause
    [TestCase("Brazil:Real")]                            // bare colon delimiter
    [TestCase("Brazil,Real")]                            // bare comma delimiter
    public void TryValidate_Fails_WithInvalidChars_WhenContainsFilterDelimiters(string currency)
    {
        var ok = CurrencyQueryValidator.TryValidate(currency, out var error);

        Assert.That(ok, Is.False);
        Assert.That(error, Does.Contain("invalid characters"));
    }

    [Test]
    public void TryValidate_Fails_WithInvalidChars_WhenContainsControlCharacter()
    {
        // Build the NUL with (char)0 so the source stays pure ASCII (no invisible bytes).
        var currency = "Brazil" + (char)0 + "Real";

        var ok = CurrencyQueryValidator.TryValidate(currency, out var error);

        Assert.That(ok, Is.False);
        Assert.That(error, Does.Contain("invalid characters"));
    }

    [Test]
    public void TryValidate_Fails_WithLengthMessage_WhenTooLong()
    {
        var currency = new string('a', CurrencyQueryValidator.MaxLength + 1);

        var ok = CurrencyQueryValidator.TryValidate(currency, out var error);

        Assert.That(ok, Is.False);
        Assert.That(error, Does.Contain("exceed"));
    }

    [TestCase("Brazil-Real")]
    [TestCase("Canada-Dollar")]
    [TestCase("Euro Zone-Euro")]              // space
    [TestCase("Trinidad & Tobago-Dollar")]   // ampersand + spaces
    [TestCase("  Brazil-Real  ")]            // trimmed before checks
    public void TryValidate_Succeeds_ForLegitimateCurrencyNames(string currency)
    {
        var ok = CurrencyQueryValidator.TryValidate(currency, out var error);

        Assert.That(ok, Is.True);
        Assert.That(error, Is.Null);
    }

    [Test]
    public void TryValidate_Succeeds_WhenExactlyMaxLength()
    {
        var currency = new string('a', CurrencyQueryValidator.MaxLength);

        var ok = CurrencyQueryValidator.TryValidate(currency, out var error);

        Assert.That(ok, Is.True);
        Assert.That(error, Is.Null);
    }
}
