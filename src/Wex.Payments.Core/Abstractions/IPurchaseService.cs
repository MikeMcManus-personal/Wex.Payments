using Wex.Payments.Core.Models;

namespace Wex.Payments.Core.Abstractions;

public interface IPurchaseService
{
    /// <summary>Stores a purchase transaction and returns it with its assigned identifier (Requirement #1).</summary>
    Task<PurchaseTransaction> StoreAsync(StorePurchaseCommand command, CancellationToken cancellationToken = default);

    /// <summary>Gets a stored purchase by identifier. Throws if it does not exist.</summary>
    Task<PurchaseTransaction> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a stored purchase converted into the target currency using the exchange rate
    /// active for the purchase date (Requirement #2). Throws if the purchase does not exist
    /// or if no rate is available within the 6-month window on or before the purchase date.
    /// </summary>
    Task<ConvertedPurchase> GetConvertedAsync(Guid id, string countryCurrencyDesc, CancellationToken cancellationToken = default);
}
