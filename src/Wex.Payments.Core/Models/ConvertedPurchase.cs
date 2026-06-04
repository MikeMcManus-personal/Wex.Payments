namespace Wex.Payments.Core.Models;

/// <summary>
/// A stored purchase retrieved and converted into a target currency (Requirement #2).
/// Includes every field the brief mandates: identifier, description, transaction date,
/// original USD amount, the exchange rate used, and the converted amount.
/// </summary>
public sealed record ConvertedPurchase(
    Guid Id,
    string Description,
    DateOnly TransactionDate,
    decimal OriginalAmountUsd,
    string CountryCurrencyDesc,
    decimal ExchangeRate,
    DateOnly ExchangeRateDate,
    decimal ConvertedAmount);
