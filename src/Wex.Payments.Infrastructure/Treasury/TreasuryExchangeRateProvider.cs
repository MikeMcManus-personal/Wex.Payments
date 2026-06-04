using System.Globalization;
using Microsoft.Extensions.Logging;
using Refit;
using Wex.Payments.Core.Abstractions;
using Wex.Payments.Core.Exceptions;
using Wex.Payments.Core.Models;

namespace Wex.Payments.Infrastructure.Treasury;

internal sealed class TreasuryExchangeRateProvider : IExchangeRateProvider
{
    private const string Fields = "country_currency_desc,exchange_rate,record_date";
    private const string Sort = "-record_date";

    private readonly ITreasuryFiscalDataApi _api;
    private readonly ILogger<TreasuryExchangeRateProvider> _logger;

    public TreasuryExchangeRateProvider(
        ITreasuryFiscalDataApi api,
        ILogger<TreasuryExchangeRateProvider> logger)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ExchangeRate?> GetLatestRateOnOrBeforeAsync(
        string countryCurrencyDesc,
        DateOnly onOrBefore,
        DateOnly notBefore,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCurrencyDesc);

        var filter = string.Create(CultureInfo.InvariantCulture,
            $"country_currency_desc:eq:{countryCurrencyDesc},record_date:lte:{onOrBefore:yyyy-MM-dd},record_date:gte:{notBefore:yyyy-MM-dd}");

        _logger.LogInformation(
            "Calling Treasury rates_of_exchange filter={Filter}", filter);

        try
        {
            var response = await _api
                .GetRatesOfExchangeAsync(Fields, filter, Sort, pageSize: 1, cancellationToken)
                .ConfigureAwait(false);

            var record = response?.Data?.FirstOrDefault();
            if (record is null)
            {
                _logger.LogInformation(
                    "Treasury returned no rates for {Currency} in window", countryCurrencyDesc);
                return null;
            }

            if (!decimal.TryParse(record.ExchangeRate, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
            {
                throw new ExchangeRateProviderException(
                    $"Treasury returned non-numeric exchange_rate '{record.ExchangeRate}' for {countryCurrencyDesc}.");
            }

            if (!DateOnly.TryParseExact(record.RecordDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var recordDate))
            {
                throw new ExchangeRateProviderException(
                    $"Treasury returned non-parseable record_date '{record.RecordDate}' for {countryCurrencyDesc}.");
            }

            return new ExchangeRate(record.CountryCurrencyDesc, rate, recordDate);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex,
                "Treasury API returned {StatusCode} for {Currency}", ex.StatusCode, countryCurrencyDesc);
            throw new ExchangeRateProviderException(
                $"Treasury API returned status {(int)ex.StatusCode} ({ex.StatusCode}).", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Treasury HTTP request failed for {Currency}", countryCurrencyDesc);
            throw new ExchangeRateProviderException("Treasury HTTP request failed.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Treasury request timed out for {Currency}", countryCurrencyDesc);
            throw new ExchangeRateProviderException("Treasury request timed out.", ex);
        }
    }
}
