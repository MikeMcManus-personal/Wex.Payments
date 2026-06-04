using System.Text.Json;
using Wex.Payments.Api.Contracts;

namespace Wex.Payments.UnitTests.Serialization;

[TestFixture]
public sealed class MoneyJsonConverterTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Test]
    public void PurchaseResponse_SerializesAmount_WithTwoDecimals()
    {
        var json = JsonSerializer.Serialize(
            new PurchaseResponse(Guid.NewGuid(), "x", new DateOnly(2024, 1, 1), 100m), Web);

        Assert.That(json, Does.Contain("\"amountUsd\":100.00"));
    }

    [Test]
    public void ConvertedResponse_MoneyIsTwoDecimals_ButRateKeepsPrecision()
    {
        var json = JsonSerializer.Serialize(
            new ConvertedPurchaseResponse(
                Id: Guid.NewGuid(),
                Description: "x",
                TransactionDate: new DateOnly(2024, 1, 1),
                OriginalAmountUsd: 100m,
                CountryCurrencyDesc: "X-Y",
                ExchangeRate: 9.5m,
                ExchangeRateDate: new DateOnly(2024, 1, 1),
                ConvertedAmount: 550m),
            Web);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"originalAmountUsd\":100.00"));
            Assert.That(json, Does.Contain("\"convertedAmount\":550.00"));
            // Rate must NOT be coerced to 2dp.
            Assert.That(json, Does.Contain("\"exchangeRate\":9.5"));
        });
    }
}
