using Microsoft.Extensions.Logging;
using Wex.Payments.Core.Abstractions;
using Wex.Payments.Core.Exceptions;
using Wex.Payments.Core.Models;

namespace Wex.Payments.Core.Services;

public sealed class PurchaseService : IPurchaseService
{
    /// <summary>The brief allows a rate dated up to 6 months before the purchase date.</summary>
    private const int RateLookbackMonths = 6;

    /// <summary>
    /// The brief says "rounded to two decimal places (i.e., cent)" but is silent on the mode.
    /// We round half away from zero (the intuitive "round to nearest cent"), once, at the end.
    /// See README "Financial rigor" for the rationale and the alternative considered.
    /// </summary>
    private const MidpointRounding Rounding = MidpointRounding.AwayFromZero;

    private readonly IPurchaseTransactionRepository _repository;
    private readonly IExchangeRateProvider _rateProvider;
    private readonly ILogger<PurchaseService> _logger;

    public PurchaseService(
        IPurchaseTransactionRepository repository,
        IExchangeRateProvider rateProvider,
        ILogger<PurchaseService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _rateProvider = rateProvider ?? throw new ArgumentNullException(nameof(rateProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PurchaseTransaction> StoreAsync(StorePurchaseCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var transaction = new PurchaseTransaction(
            id: Guid.NewGuid(),
            description: command.Description.Trim(),
            transactionDate: command.TransactionDate,
            amountUsd: decimal.Round(command.AmountUsd, 2, Rounding));

        await _repository.AddAsync(transaction, cancellationToken).ConfigureAwait(false);

        // Keep the monetary amount out of Information-level logs; id + date are sufficient
        // for audit there, and the amount is available at Debug when diagnosing.
        _logger.LogInformation(
            "Stored purchase {Id} on {Date}",
            transaction.Id, transaction.TransactionDate);
        _logger.LogDebug(
            "Purchase {Id} amount {Amount:F2} USD",
            transaction.Id, transaction.AmountUsd);

        return transaction;
    }

    public async Task<PurchaseTransaction> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return transaction ?? throw new PurchaseTransactionNotFoundException(id);
    }

    public async Task<ConvertedPurchase> GetConvertedAsync(Guid id, string countryCurrencyDesc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCurrencyDesc);

        var transaction = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        var currency = countryCurrencyDesc.Trim();
        var earliest = transaction.TransactionDate.AddMonths(-RateLookbackMonths);

        _logger.LogInformation(
            "Converting purchase {Id} to {Currency}; rate must fall in [{Earliest}..{Date}]",
            transaction.Id, currency, earliest, transaction.TransactionDate);

        var rate = await _rateProvider
            .GetLatestRateOnOrBeforeAsync(currency, transaction.TransactionDate, earliest, cancellationToken)
            .ConfigureAwait(false);

        if (rate is null)
        {
            _logger.LogWarning(
                "No rate for purchase {Id} ({Currency}) in window [{Earliest}..{Date}]",
                transaction.Id, currency, earliest, transaction.TransactionDate);
            throw new ExchangeRateNotFoundException(currency, transaction.TransactionDate);
        }

        var convertedAmount = decimal.Round(transaction.AmountUsd * rate.Rate, 2, Rounding);

        return new ConvertedPurchase(
            Id: transaction.Id,
            Description: transaction.Description,
            TransactionDate: transaction.TransactionDate,
            OriginalAmountUsd: transaction.AmountUsd,
            CountryCurrencyDesc: rate.CountryCurrencyDesc,
            ExchangeRate: rate.Rate,
            ExchangeRateDate: rate.RecordDate,
            ConvertedAmount: convertedAmount);
    }
}
