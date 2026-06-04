using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Wex.Payments.Core.Exceptions;

namespace Wex.Payments.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (PurchaseTransactionNotFoundException ex)
        {
            _logger.LogInformation(ex, "Purchase {Id} not found", ex.Id);
            await WriteProblemAsync(context, StatusCodes.Status404NotFound,
                title: "Purchase not found",
                detail: ex.Message,
                extensions: new Dictionary<string, object?> { ["purchaseId"] = ex.Id });
        }
        catch (ExchangeRateNotFoundException ex)
        {
            // Business error: well-formed request that cannot be fulfilled (no rate in window).
            _logger.LogInformation(ex, "No exchange rate available for request");
            await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity,
                title: "Purchase cannot be converted",
                detail: ex.Message,
                extensions: new Dictionary<string, object?>
                {
                    ["countryCurrencyDesc"] = ex.CountryCurrencyDesc,
                    ["transactionDate"] = ex.TransactionDate.ToString("yyyy-MM-dd"),
                });
        }
        catch (ExchangeRateProviderException ex)
        {
            _logger.LogError(ex, "Upstream Treasury provider error");
            await WriteProblemAsync(context, StatusCodes.Status502BadGateway,
                title: "Upstream provider error",
                detail: ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request was cancelled by the client");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                title: "Unexpected error",
                detail: "An unexpected error occurred. See server logs for details.");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        IDictionary<string, object?>? extensions = null)
    {
        if (context.Response.HasStarted) return;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (extensions is not null)
        {
            foreach (var kvp in extensions)
            {
                problem.Extensions[kvp.Key] = kvp.Value;
            }
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOpts));
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
}
