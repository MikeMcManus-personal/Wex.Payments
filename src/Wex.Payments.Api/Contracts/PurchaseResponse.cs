using System.Text.Json.Serialization;
using Wex.Payments.Api.Serialization;

namespace Wex.Payments.Api.Contracts;

/// <summary>Representation of a stored purchase transaction.</summary>
public sealed record PurchaseResponse(
    Guid Id,
    string Description,
    DateOnly TransactionDate,
    [property: JsonConverter(typeof(MoneyJsonConverter))] decimal AmountUsd);
