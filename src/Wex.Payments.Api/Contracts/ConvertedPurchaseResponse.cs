namespace Wex.Payments.Api.Contracts;

/// <summary>
/// A stored purchase converted to a target currency (Requirement #2). Contains every
/// field the brief requires the retrieval to include.
/// </summary>
public sealed record ConvertedPurchaseResponse(
    Guid Id,
    string Description,
    DateOnly TransactionDate,
    decimal OriginalAmountUsd,
    string CountryCurrencyDesc,
    decimal ExchangeRate,
    DateOnly ExchangeRateDate,
    decimal ConvertedAmount);
