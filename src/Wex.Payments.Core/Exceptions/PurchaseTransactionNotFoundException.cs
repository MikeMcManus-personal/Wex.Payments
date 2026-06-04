namespace Wex.Payments.Core.Exceptions;

/// <summary>
/// The requested purchase transaction identifier does not exist.
/// Maps to HTTP 404 Not Found.
/// </summary>
public sealed class PurchaseTransactionNotFoundException : Exception
{
    public Guid Id { get; }

    public PurchaseTransactionNotFoundException(Guid id)
        : base($"Purchase transaction '{id}' was not found.")
    {
        Id = id;
    }
}
