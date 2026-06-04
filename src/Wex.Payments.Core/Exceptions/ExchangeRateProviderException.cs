namespace Wex.Payments.Core.Exceptions;

public sealed class ExchangeRateProviderException : Exception
{
    public ExchangeRateProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ExchangeRateProviderException(string message) : base(message)
    {
    }
}
