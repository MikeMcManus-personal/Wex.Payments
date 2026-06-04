namespace Wex.Payments.Api.Contracts;

/// <summary>Representation of a stored purchase transaction.</summary>
public sealed record PurchaseResponse(
    Guid Id,
    string Description,
    DateOnly TransactionDate,
    decimal AmountUsd);
