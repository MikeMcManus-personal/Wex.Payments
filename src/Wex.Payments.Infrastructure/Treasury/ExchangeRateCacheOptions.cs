using System.ComponentModel.DataAnnotations;

namespace Wex.Payments.Infrastructure.Treasury;

/// <summary>
/// Tunables for <see cref="CachingExchangeRateProvider"/>. Sensible defaults apply when
/// the configuration section is absent, so caching works out of the box.
/// </summary>
public sealed class ExchangeRateCacheOptions
{
    public const string SectionName = "ExchangeRateCache";

    /// <summary>
    /// TTL for rates whose <c>record_date</c> is recent (still amendment-prone) or whose
    /// purchase date sits near a quarter boundary. Kept short so a new quarterly publish
    /// or an amendment is picked up promptly.
    /// </summary>
    [Range(1, 1440)]
    public int RecentTtlMinutes { get; set; } = 60;

    /// <summary>
    /// TTL for rates from a quarter already superseded by a newer published quarter. The
    /// answer is effectively frozen, so this can be long.
    /// </summary>
    [Range(1, 168)]
    public int HistoricalTtlHours { get; set; } = 24;

    /// <summary>
    /// TTL for "no rate in the 6-month window" results. Short, because a future publish or
    /// amendment could create a rate.
    /// </summary>
    [Range(1, 1440)]
    public int NegativeTtlMinutes { get; set; } = 30;

    /// <summary>
    /// A <c>record_date</c> or purchase date within this many days of today is treated as
    /// "recent". Defaults to roughly one quarter plus publication lag so boundary lookups
    /// are never cached across a publish.
    /// </summary>
    [Range(1, 366)]
    public int RecentWindowDays { get; set; } = 120;

    internal TimeSpan RecentTtl => TimeSpan.FromMinutes(RecentTtlMinutes);

    internal TimeSpan HistoricalTtl => TimeSpan.FromHours(HistoricalTtlHours);

    internal TimeSpan NegativeTtl => TimeSpan.FromMinutes(NegativeTtlMinutes);
}
