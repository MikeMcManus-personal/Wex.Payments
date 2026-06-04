namespace Wex.Payments.Api.Contracts;

/// <summary>Request body for storing a purchase transaction (Requirement #1).</summary>
public sealed record StorePurchaseRequest(
    string Description,
    DateOnly TransactionDate,
    decimal AmountUsd);
