using Wex.Payments.Core.Models;

namespace Wex.Payments.Core.Abstractions;

/// <summary>
/// Persistence boundary for purchase transactions. The default implementation is
/// in-memory so the app is "plug and play" with no external database dependency,
/// but this abstraction allows a durable store to be substituted without touching
/// the domain.
/// </summary>
public interface IPurchaseTransactionRepository
{
    Task AddAsync(PurchaseTransaction transaction, CancellationToken cancellationToken = default);

    Task<PurchaseTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
