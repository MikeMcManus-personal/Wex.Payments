using Wex.Payments.Core.Abstractions;
using Wex.Payments.Core.Models;

namespace Wex.Payments.IntegrationTests.Fakes;

internal sealed class FakeExchangeRateProvider : IExchangeRateProvider
{
    public Func<string, DateOnly, DateOnly, Task<ExchangeRate?>> Handler { get; set; } =
        (_, _, _) => Task.FromResult<ExchangeRate?>(null);

    public Task<ExchangeRate?> GetLatestRateOnOrBeforeAsync(
        string countryCurrencyDesc,
        DateOnly onOrBefore,
        DateOnly notBefore,
        CancellationToken cancellationToken = default) =>
        Handler(countryCurrencyDesc, onOrBefore, notBefore);
}
