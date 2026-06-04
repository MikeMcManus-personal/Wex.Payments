using Wex.Payments.Api.Contracts;
using Wex.Payments.Api.Validation;

namespace Wex.Payments.UnitTests.Validation;

[TestFixture]
public sealed class StorePurchaseRequestValidatorTests
{
    private StorePurchaseRequestValidator _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new StorePurchaseRequestValidator();

    [Test]
    public void Validate_Passes_ForGoodRequest()
    {
        var request = new StorePurchaseRequest("Coffee", new DateOnly(2024, 6, 30), 12.34m);

        var result = _sut.Validate(request);

        Assert.That(result.IsValid, Is.True, () => string.Join(";", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Test]
    public void Validate_Passes_ForFutureDate()
    {
        // The brief only mandates a valid date format; future dates are allowed at storage.
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);
        var request = new StorePurchaseRequest("x", future, 1.00m);

        Assert.That(_sut.Validate(request).IsValid, Is.True);
    }

    [Test]
    public void Validate_Fails_WhenDescriptionExceeds50Chars()
    {
        var request = new StorePurchaseRequest(new string('a', 51), new DateOnly(2024, 6, 30), 1.00m);

        var result = _sut.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(StorePurchaseRequest.Description)));
    }

    [Test]
    public void Validate_Passes_WhenDescriptionExactly50Chars()
    {
        var request = new StorePurchaseRequest(new string('a', 50), new DateOnly(2024, 6, 30), 1.00m);

        Assert.That(_sut.Validate(request).IsValid, Is.True);
    }

    [Test]
    public void Validate_Passes_When50CharsWithSurroundingWhitespace()
    {
        // Trimmed length is 50, matching what the service stores after trimming.
        var request = new StorePurchaseRequest("  " + new string('a', 50) + "  ", new DateOnly(2024, 6, 30), 1.00m);

        Assert.That(_sut.Validate(request).IsValid, Is.True);
    }

    [Test]
    public void Validate_Fails_WhenDescriptionEmpty()
    {
        var request = new StorePurchaseRequest("", new DateOnly(2024, 6, 30), 1.00m);

        var result = _sut.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(StorePurchaseRequest.Description)));
    }

    [TestCase(0)]
    [TestCase(-1.00)]
    public void Validate_Fails_WhenAmountNotPositive(decimal amount)
    {
        var request = new StorePurchaseRequest("x", new DateOnly(2024, 6, 30), amount);

        var result = _sut.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(StorePurchaseRequest.AmountUsd)));
    }

    [Test]
    public void Validate_Fails_WhenAmountHasMoreThanTwoDecimals()
    {
        var request = new StorePurchaseRequest("x", new DateOnly(2024, 6, 30), 10.005m);

        var result = _sut.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(StorePurchaseRequest.AmountUsd)));
    }
}
