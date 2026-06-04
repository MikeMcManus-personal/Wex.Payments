using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Wex.Payments.Core.Abstractions;
using Wex.Payments.Core.Models;
using Wex.Payments.Infrastructure.Treasury;

namespace Wex.Payments.UnitTests.Infrastructure;

[TestFixture]
public sealed class CachingExchangeRateProviderTests
{
    private static readonly DateOnly OnOrBefore = new(2024, 6, 30);
    private static readonly DateOnly NotBefore = new(2023, 12, 30);

    private Mock<IExchangeRateProvider> _innerMock = null!;
    private MemoryCache _cache = null!;
    private CachingExchangeRateProvider _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _innerMock = new Mock<IExchangeRateProvider>(MockBehavior.Strict);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new CachingExchangeRateProvider(
            _innerMock.Object,
            _cache,
            Options.Create(new ExchangeRateCacheOptions()),
            NullLogger<CachingExchangeRateProvider>.Instance);
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public async Task RepeatedIdenticalLookup_QueriesTreasuryOnce_AndReturnsSameRate()
    {
        var rate = new ExchangeRate("Brazil-Real", 5.165m, new DateOnly(2024, 3, 31));
        _innerMock
            .Setup(p => p.GetLatestRateOnOrBeforeAsync("Brazil-Real", OnOrBefore, NotBefore, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rate);

        var first = await _sut.GetLatestRateOnOrBeforeAsync("Brazil-Real", OnOrBefore, NotBefore);
        var second = await _sut.GetLatestRateOnOrBeforeAsync("Brazil-Real", OnOrBefore, NotBefore);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.SameAs(rate));
            Assert.That(second, Is.SameAs(rate));
        });
        _innerMock.Verify(
            p => p.GetLatestRateOnOrBeforeAsync("Brazil-Real", OnOrBefore, NotBefore, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task DifferentCurrency_IsCachedSeparately()
    {
        _innerMock
            .Setup(p => p.GetLatestRateOnOrBeforeAsync(
                It.IsAny<string>(), OnOrBefore, NotBefore, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate("X-Y", 1.23m, new DateOnly(2024, 3, 31)));

        await _sut.GetLatestRateOnOrBeforeAsync("Brazil-Real", OnOrBefore, NotBefore);
        await _sut.GetLatestRateOnOrBeforeAsync("Canada-Dollar", OnOrBefore, NotBefore);

        _innerMock.Verify(
            p => p.GetLatestRateOnOrBeforeAsync(
                It.IsAny<string>(), OnOrBefore, NotBefore, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Test]
    public async Task NegativeResult_IsCached_AndNotRequeried()
    {
        _innerMock
            .Setup(p => p.GetLatestRateOnOrBeforeAsync(
                "Atlantis-Crystal", OnOrBefore, NotBefore, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);

        var first = await _sut.GetLatestRateOnOrBeforeAsync("Atlantis-Crystal", OnOrBefore, NotBefore);
        var second = await _sut.GetLatestRateOnOrBeforeAsync("Atlantis-Crystal", OnOrBefore, NotBefore);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Null);
            Assert.That(second, Is.Null);
        });
        _innerMock.Verify(
            p => p.GetLatestRateOnOrBeforeAsync(
                "Atlantis-Crystal", OnOrBefore, NotBefore, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task CurrentQuarterPurchase_BypassesCache_AlwaysQueriesTreasury()
    {
        // 'today' is in Q2 2026; the purchase falls in the same (current, unpublished) quarter.
        var today = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var purchaseDate = new DateOnly(2026, 5, 15);
        var notBefore = purchaseDate.AddMonths(-6);

        var sut = new CachingExchangeRateProvider(
            _innerMock.Object,
            _cache,
            Options.Create(new ExchangeRateCacheOptions()),
            NullLogger<CachingExchangeRateProvider>.Instance,
            new FixedTimeProvider(today));

        _innerMock
            .Setup(p => p.GetLatestRateOnOrBeforeAsync(
                "Brazil-Real", purchaseDate, notBefore, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate("Brazil-Real", 5.254m, new DateOnly(2026, 3, 31)));

        await sut.GetLatestRateOnOrBeforeAsync("Brazil-Real", purchaseDate, notBefore);
        await sut.GetLatestRateOnOrBeforeAsync("Brazil-Real", purchaseDate, notBefore);

        // Current quarter is never cached: both calls reach Treasury.
        _innerMock.Verify(
            p => p.GetLatestRateOnOrBeforeAsync(
                "Brazil-Real", purchaseDate, notBefore, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
