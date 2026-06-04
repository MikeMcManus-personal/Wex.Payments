using Wex.Payments.Api.Contracts;
using Wex.Payments.Api.Validation;
using Wex.Payments.Core.Abstractions;
using Wex.Payments.Core.Models;

namespace Wex.Payments.Api.Endpoints;

public static class PurchaseEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/purchases").WithTags("Purchases");

        // Requirement #1 — store a purchase transaction.
        group.MapPost("/", StoreAsync)
            .AddEndpointFilter<ValidationEndpointFilter<StorePurchaseRequest>>()
            .WithName("StorePurchase")
            .WithSummary("Store a purchase transaction in USD and assign it a unique identifier.")
            .Produces<PurchaseResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        // Retrieve the raw stored purchase.
        group.MapGet("/{id:guid}", GetAsync)
            .WithName("GetPurchase")
            .WithSummary("Retrieve a stored purchase transaction by identifier.")
            .Produces<PurchaseResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Requirement #2 — retrieve a stored purchase converted to a target currency.
        group.MapGet("/{id:guid}/converted", GetConvertedAsync)
            .WithName("GetConvertedPurchase")
            .WithSummary("Retrieve a stored purchase converted to a target currency using the rate active for the purchase date.")
            .Produces<ConvertedPurchaseResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return app;
    }

    private static async Task<IResult> StoreAsync(
        StorePurchaseRequest request,
        IPurchaseService service,
        CancellationToken cancellationToken)
    {
        var command = new StorePurchaseCommand(
            request.Description.Trim(),
            request.TransactionDate,
            request.AmountUsd);

        var stored = await service.StoreAsync(command, cancellationToken);

        var response = ToResponse(stored);
        return Results.CreatedAtRoute("GetPurchase", new { id = stored.Id }, response);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IPurchaseService service,
        CancellationToken cancellationToken)
    {
        var stored = await service.GetAsync(id, cancellationToken);
        return Results.Ok(ToResponse(stored));
    }

    private static async Task<IResult> GetConvertedAsync(
        Guid id,
        string? currency,
        IPurchaseService service,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["currency"] = ["The 'currency' query parameter is required (e.g. 'Canada-Dollar')."],
            });
        }

        var converted = await service.GetConvertedAsync(id, currency, cancellationToken);

        var response = new ConvertedPurchaseResponse(
            Id: converted.Id,
            Description: converted.Description,
            TransactionDate: converted.TransactionDate,
            OriginalAmountUsd: converted.OriginalAmountUsd,
            CountryCurrencyDesc: converted.CountryCurrencyDesc,
            ExchangeRate: converted.ExchangeRate,
            ExchangeRateDate: converted.ExchangeRateDate,
            ConvertedAmount: converted.ConvertedAmount);

        return Results.Ok(response);
    }

    private static PurchaseResponse ToResponse(PurchaseTransaction t) =>
        new(t.Id, t.Description, t.TransactionDate, t.AmountUsd);
}
