using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Refit;
using Wex.Payments.Core.Abstractions;
using Wex.Payments.Infrastructure.Treasury;

namespace Wex.Payments.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddWexPaymentsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TreasuryOptions>()
            .Bind(configuration.GetSection(TreasuryOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var treasury = configuration.GetSection(TreasuryOptions.SectionName).Get<TreasuryOptions>()
            ?? new TreasuryOptions();

        services.AddRefitClient<ITreasuryFiscalDataApi>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<TreasuryOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl);
                // The resilience pipeline owns timeouts (per-attempt + total), so let it govern.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            // Polly v8 standard pipeline: rate limiter, total timeout, retry, attempt timeout,
            // circuit breaker. Replaces the legacy Polly.Extensions.Http wiring.
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = treasury.RetryCount;
                options.Retry.Delay = TimeSpan.FromMilliseconds(treasury.RetryBaseDelayMs);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                // TimeoutSeconds is the per-attempt budget; the total must exceed a single
                // attempt, and the breaker's sampling window must be >= 2x the attempt timeout.
                var attempt = TimeSpan.FromSeconds(treasury.TimeoutSeconds);
                options.AttemptTimeout.Timeout = attempt;
                options.TotalRequestTimeout.Timeout =
                    TimeSpan.FromSeconds(treasury.TimeoutSeconds * (treasury.RetryCount + 2));
                options.CircuitBreaker.SamplingDuration =
                    TimeSpan.FromSeconds(Math.Max(treasury.TimeoutSeconds * 2, 30));
            });

        services.AddMemoryCache();

        services.AddOptions<ExchangeRateCacheOptions>()
            .Bind(configuration.GetSection(ExchangeRateCacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Decorate the Treasury provider with an in-memory cache. Rates are quarterly and
        // immutable once published, so this turns repeated identical lookups into cache hits
        // and shields the Treasury API from amplified load or rate limiting.
        services.AddScoped<TreasuryExchangeRateProvider>();
        services.AddScoped<IExchangeRateProvider>(sp =>
            new CachingExchangeRateProvider(
                sp.GetRequiredService<TreasuryExchangeRateProvider>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<IOptions<ExchangeRateCacheOptions>>(),
                sp.GetRequiredService<ILogger<CachingExchangeRateProvider>>()));

        return services;
    }
}
