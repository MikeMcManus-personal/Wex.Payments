using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
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

        services.AddRefitClient<ITreasuryFiscalDataApi>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<TreasuryOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            })
            .AddPolicyHandler((sp, _) => BuildRetryPolicy(sp))
            .AddPolicyHandler(BuildCircuitBreakerPolicy());

        services.AddMemoryCache();

        services.AddOptions<ExchangeRateCacheOptions>()
            .Bind(configuration.GetSection(ExchangeRateCacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Decorate the Treasury provider with an in-memory cache. Rates are quarterly and
        // immutable once published, so this turns repeated identical lookups into cache hits
        // and shields the Treasury API (and our request quota) from amplified load.
        services.AddScoped<TreasuryExchangeRateProvider>();
        services.AddScoped<IExchangeRateProvider>(sp =>
            new CachingExchangeRateProvider(
                sp.GetRequiredService<TreasuryExchangeRateProvider>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<IOptions<ExchangeRateCacheOptions>>(),
                sp.GetRequiredService<ILogger<CachingExchangeRateProvider>>()));

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(IServiceProvider sp)
    {
        var opts = sp.GetRequiredService<IOptions<TreasuryOptions>>().Value;
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: opts.RetryCount,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(opts.RetryBaseDelayMs * Math.Pow(2, attempt - 1)));
    }

    private static IAsyncPolicy<HttpResponseMessage> BuildCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
}
