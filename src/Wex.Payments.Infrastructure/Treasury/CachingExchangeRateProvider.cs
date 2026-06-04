using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wex.Payments.Core.Abstractions;
using Wex.Payments.Core.Models;

namespace Wex.Payments.Infrastructure.Treasury;

/// <summary>
/// In-memory caching decorator over <see cref="IExchangeRateProvider"/>.
/// <para>
/// The Treasury rates_of_exchange dataset is quarterly and immutable once published
/// (recent quarters may still be amended), so identical lookups dominate and cache hit
/// rates are high. The TTL is chosen from the resolved <c>record_date</c>:
/// </para>
/// <list type="bullet">
/// <item>Current (unpublished) quarter or future-dated -&gt; bypass; always hit Treasury.</item>
/// <item>Historical (a newer quarter has already published) -&gt; long TTL; the answer is frozen.</item>
/// <item>Recent record_date, or a purchase date near a quarter boundary -&gt; short TTL.</item>
/// <item>No rate in the 6-month window -&gt; brief negative cache.</item>
/// </list>
/// <para>
/// This is a deliberately simple L1 (in-process) cache. Under a scaled-out deployment
/// each instance keeps its own copy; the natural upgrade is .NET's HybridCache with an
/// L2 (e.g. Redis), which slots in behind this same interface.
/// </para>
/// </summary>
internal sealed class CachingExchangeRateProvider : IExchangeRateProvider
{
    private readonly IExchangeRateProvider _inner;
    private readonly IMemoryCache _cache;
    private readonly ExchangeRateCacheOptions _options;
    private readonly ILogger<CachingExchangeRateProvider> _logger;
    private readonly TimeProvider _timeProvider;

    public CachingExchangeRateProvider(
        IExchangeRateProvider inner,
        IMemoryCache cache,
        IOptions<ExchangeRateCacheOptions> options,
        ILogger<CachingExchangeRateProvider> logger,
        TimeProvider? timeProvider = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ExchangeRate?> GetLatestRateOnOrBeforeAsync(
        string countryCurrencyDesc,
        DateOnly onOrBefore,
        DateOnly notBefore,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        // Current (not-yet-published) quarter or a future-dated purchase: the applicable
        // rate is still settling, so go straight to Treasury and neither read nor write
        // the cache.
        if (onOrBefore >= StartOfQuarter(today))
        {
            _logger.LogDebug(
                "Bypassing exchange-rate cache for {Currency} <= {Date} (current/unpublished quarter)",
                countryCurrencyDesc, onOrBefore);
            return await _inner
                .GetLatestRateOnOrBeforeAsync(countryCurrencyDesc, onOrBefore, notBefore, cancellationToken)
                .ConfigureAwait(false);
        }

        var key = new CacheKey(countryCurrencyDesc, onOrBefore, notBefore);

        if (_cache.TryGetValue(key, out CacheEntry? cached) && cached is not null)
        {
            _logger.LogDebug(
                "Exchange-rate cache hit for {Currency} <= {Date}", countryCurrencyDesc, onOrBefore);
            return cached.Rate;
        }

        var rate = await _inner
            .GetLatestRateOnOrBeforeAsync(countryCurrencyDesc, onOrBefore, notBefore, cancellationToken)
            .ConfigureAwait(false);

        var ttl = ComputeTtl(rate, onOrBefore, today);
        _cache.Set(key, new CacheEntry(rate), ttl);

        _logger.LogDebug(
            "Exchange-rate cache miss for {Currency} <= {Date}; cached {Result} for {Ttl}",
            countryCurrencyDesc, onOrBefore, rate is null ? "negative" : "rate", ttl);

        return rate;
    }

    internal TimeSpan ComputeTtl(ExchangeRate? rate, DateOnly onOrBefore, DateOnly today)
    {
        // No rate in the window: a future publish/amendment could create one -> short.
        if (rate is null)
        {
            return _options.NegativeTtl;
        }

        var recentWindow = _options.RecentWindowDays;

        // Volatile if the rate's quarter may still be amended, OR the purchase date is near
        // enough to "now" that a not-yet-published quarter could later become eligible.
        var rateIsRecent = today.DayNumber - rate.RecordDate.DayNumber <= recentWindow;
        var purchaseIsRecent = onOrBefore.DayNumber >= today.DayNumber - recentWindow;

        return rateIsRecent || purchaseIsRecent ? _options.RecentTtl : _options.HistoricalTtl;
    }

    // Calendar quarter start for the given date (matches Treasury's record_date cadence:
    // Mar 31, Jun 30, Sep 30, Dec 31).
    private static DateOnly StartOfQuarter(DateOnly date) =>
        new(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1);

    // A dedicated key type namespaces our entries within the shared IMemoryCache and gives
    // value equality for free.
    private readonly record struct CacheKey(string Currency, DateOnly OnOrBefore, DateOnly NotBefore);

    // Holder so a cached "no rate" (Rate is null) is distinguishable from a cache miss.
    private sealed record CacheEntry(ExchangeRate? Rate);
}
