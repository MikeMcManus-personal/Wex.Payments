using System.ComponentModel.DataAnnotations;

namespace Wex.Payments.Infrastructure.Treasury;

public sealed class TreasuryOptions
{
    public const string SectionName = "Treasury";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.fiscaldata.treasury.gov/";

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(0, 10)]
    public int RetryCount { get; set; } = 3;

    [Range(50, 10000)]
    public int RetryBaseDelayMs { get; set; } = 200;
}
