namespace Wex.Payments.Core.Models;

/// <summary>
/// A stored purchase transaction in United States dollars (Requirement #1).
/// Immutable once created; the identifier is assigned at construction time.
/// </summary>
public sealed class PurchaseTransaction
{
    public Guid Id { get; }
    public string Description { get; }
    public DateOnly TransactionDate { get; }
    public decimal AmountUsd { get; }

    public PurchaseTransaction(Guid id, string description, DateOnly transactionDate, decimal amountUsd)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Identifier must be non-empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (description.Length > 50)
        {
            throw new ArgumentException("Description must not exceed 50 characters.", nameof(description));
        }

        if (amountUsd <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amountUsd), "Purchase amount must be positive.");
        }

        Id = id;
        Description = description;
        TransactionDate = transactionDate;
        AmountUsd = amountUsd;
    }
}
