using System.Text.Json.Serialization;
using Wex.Payments.Api.Serialization;

namespace Wex.Payments.Api.Contracts;

/// <summary>
/// A stored purchase converted to a target currency (Requirement #2). Contains every
/// field the brief requires the retrieval to include. Money fields render at two
/// decimals; <see cref="ExchangeRate"/> keeps its native (variable) precision.
/// </summary>
public sealed record ConvertedPurchaseResponse(
    Guid Id,
    string Description,
    DateOnly TransactionDate,
    [property: JsonConverter(typeof(MoneyJsonConverter))] decimal OriginalAmountUsd,
    string CountryCurrencyDesc,
    decimal ExchangeRate,
    DateOnly ExchangeRateDate,
    [property: JsonConverter(typeof(MoneyJsonConverter))] decimal ConvertedAmount);
