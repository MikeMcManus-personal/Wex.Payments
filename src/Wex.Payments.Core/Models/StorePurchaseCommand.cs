namespace Wex.Payments.Core.Models;

/// <summary>
/// Input to store a new purchase transaction (Requirement #1).
/// </summary>
public sealed record StorePurchaseCommand(
    string Description,
    DateOnly TransactionDate,
    decimal AmountUsd);
