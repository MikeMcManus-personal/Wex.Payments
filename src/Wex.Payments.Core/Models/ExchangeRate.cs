namespace Wex.Payments.Core.Models;

public sealed record ExchangeRate(
    string CountryCurrencyDesc,
    decimal Rate,
    DateOnly RecordDate);
