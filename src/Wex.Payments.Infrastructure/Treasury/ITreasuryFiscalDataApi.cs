using Refit;
using Wex.Payments.Infrastructure.Treasury.Models;

namespace Wex.Payments.Infrastructure.Treasury;

internal interface ITreasuryFiscalDataApi
{
    [Get("/services/api/fiscal_service/v1/accounting/od/rates_of_exchange")]
    Task<RatesOfExchangeResponse> GetRatesOfExchangeAsync(
        [AliasAs("fields")] string fields,
        [AliasAs("filter")] string filter,
        [AliasAs("sort")] string sort,
        [AliasAs("page[size]")] int pageSize,
        CancellationToken cancellationToken = default);
}
