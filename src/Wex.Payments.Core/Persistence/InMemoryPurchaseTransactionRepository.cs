using System.Collections.Concurrent;
using Wex.Payments.Core.Abstractions;
using Wex.Payments.Core.Models;

namespace Wex.Payments.Core.Persistence;

/// <summary>
/// Thread-safe in-memory store. Satisfies the brief's "no external database" constraint.
/// Registered as a singleton so data persists for the lifetime of the process.
/// </summary>
public sealed class InMemoryPurchaseTransactionRepository : IPurchaseTransactionRepository
{
    private readonly ConcurrentDictionary<Guid, PurchaseTransaction> _store = new();

    public Task AddAsync(PurchaseTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!_store.TryAdd(transaction.Id, transaction))
        {
            throw new InvalidOperationException($"A purchase with id '{transaction.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<PurchaseTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var transaction);
        return Task.FromResult(transaction);
    }
}
