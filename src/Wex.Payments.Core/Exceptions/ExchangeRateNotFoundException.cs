namespace Wex.Payments.Core.Exceptions;

/// <summary>
/// Business error: no exchange rate is available within 6 months on or before the
/// purchase date, so the purchase cannot be converted to the target currency.
/// Maps to HTTP 422 Unprocessable Entity.
/// </summary>
public sealed class ExchangeRateNotFoundException : Exception
{
    public string CountryCurrencyDesc { get; }
    public DateOnly TransactionDate { get; }

    public ExchangeRateNotFoundException(string countryCurrencyDesc, DateOnly transactionDate)
        : base($"The purchase cannot be converted to the target currency '{countryCurrencyDesc}': no exchange rate is available within 6 months on or before {transactionDate:yyyy-MM-dd}.")
    {
        CountryCurrencyDesc = countryCurrencyDesc;
        TransactionDate = transactionDate;
    }
}
