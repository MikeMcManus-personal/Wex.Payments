using Wex.Payments.Core.Models;

namespace Wex.Payments.Core.Abstractions;

public interface IExchangeRateProvider
{
    Task<ExchangeRate?> GetLatestRateOnOrBeforeAsync(
        string countryCurrencyDesc,
        DateOnly onOrBefore,
        DateOnly notBefore,
        CancellationToken cancellationToken = default);
}
