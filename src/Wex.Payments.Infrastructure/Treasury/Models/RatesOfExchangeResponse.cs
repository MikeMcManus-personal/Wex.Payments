using System.Text.Json.Serialization;

namespace Wex.Payments.Infrastructure.Treasury.Models;

internal sealed class RatesOfExchangeResponse
{
    [JsonPropertyName("data")]
    public List<RateOfExchangeRecord> Data { get; init; } = new();
}

internal sealed class RateOfExchangeRecord
{
    [JsonPropertyName("country_currency_desc")]
    public string CountryCurrencyDesc { get; init; } = string.Empty;

    [JsonPropertyName("exchange_rate")]
    public string ExchangeRate { get; init; } = string.Empty;

    [JsonPropertyName("record_date")]
    public string RecordDate { get; init; } = string.Empty;
}
