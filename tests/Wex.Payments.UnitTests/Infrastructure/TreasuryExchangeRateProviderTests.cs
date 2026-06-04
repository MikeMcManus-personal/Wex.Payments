using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Refit;
using Wex.Payments.Core.Exceptions;
using Wex.Payments.Infrastructure.Treasury;
using Wex.Payments.Infrastructure.Treasury.Models;

namespace Wex.Payments.UnitTests.Infrastructure;

[TestFixture]
public sealed class TreasuryExchangeRateProviderTests
{
    private Mock<ITreasuryFiscalDataApi> _apiMock = null!;
    private TreasuryExchangeRateProvider _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _apiMock = new Mock<ITreasuryFiscalDataApi>(MockBehavior.Strict);
        _sut = new TreasuryExchangeRateProvider(_apiMock.Object, NullLogger<TreasuryExchangeRateProvider>.Instance);
    }

    [Test]
    public async Task GetLatestRateOnOrBeforeAsync_MapsResponse_WhenRecordPresent()
    {
        _apiMock
            .Setup(a => a.GetRatesOfExchangeAsync(
                It.IsAny<string>(),
                It.Is<string>(f => f.Contains("Brazil-Real")),
                It.IsAny<string>(),
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RatesOfExchangeResponse
            {
                Data = new List<RateOfExchangeRecord>
                {
                    new() { CountryCurrencyDesc = "Brazil-Real", ExchangeRate = "5.165", RecordDate = "2024-03-31" },
                },
            });

        var rate = await _sut.GetLatestRateOnOrBeforeAsync(
            "Brazil-Real",
            new DateOnly(2024, 6, 30),
            new DateOnly(2023, 12, 30));

        Assert.That(rate, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(rate!.Rate, Is.EqualTo(5.165m));
            Assert.That(rate.RecordDate, Is.EqualTo(new DateOnly(2024, 3, 31)));
            Assert.That(rate.CountryCurrencyDesc, Is.EqualTo("Brazil-Real"));
        });
    }

    [Test]
    public async Task GetLatestRateOnOrBeforeAsync_ReturnsNull_WhenNoRecords()
    {
        _apiMock
            .Setup(a => a.GetRatesOfExchangeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RatesOfExchangeResponse { Data = new List<RateOfExchangeRecord>() });

        var rate = await _sut.GetLatestRateOnOrBeforeAsync(
            "Atlantis-Crystal",
            new DateOnly(2024, 6, 30),
            new DateOnly(2023, 12, 30));

        Assert.That(rate, Is.Null);
    }

    [Test]
    public void GetLatestRateOnOrBeforeAsync_Throws_WhenRateNonNumeric()
    {
        _apiMock
            .Setup(a => a.GetRatesOfExchangeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RatesOfExchangeResponse
            {
                Data = new List<RateOfExchangeRecord>
                {
                    new() { CountryCurrencyDesc = "X-Y", ExchangeRate = "not-a-number", RecordDate = "2024-01-01" },
                },
            });

        Assert.ThrowsAsync<ExchangeRateProviderException>(() =>
            _sut.GetLatestRateOnOrBeforeAsync("X-Y", new DateOnly(2024, 6, 30), new DateOnly(2023, 12, 30)));
    }

    [Test]
    public void GetLatestRateOnOrBeforeAsync_Throws_WhenDateMalformed()
    {
        _apiMock
            .Setup(a => a.GetRatesOfExchangeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RatesOfExchangeResponse
            {
                Data = new List<RateOfExchangeRecord>
                {
                    new() { CountryCurrencyDesc = "X-Y", ExchangeRate = "1.0", RecordDate = "junk" },
                },
            });

        Assert.ThrowsAsync<ExchangeRateProviderException>(() =>
            _sut.GetLatestRateOnOrBeforeAsync("X-Y", new DateOnly(2024, 6, 30), new DateOnly(2023, 12, 30)));
    }

    [Test]
    public async Task GetLatestRateOnOrBeforeAsync_WrapsApiException()
    {
        var apiException = await ApiException.Create(
            new HttpRequestMessage(HttpMethod.Get, "https://api.fiscaldata.treasury.gov/"),
            HttpMethod.Get,
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new RefitSettings());

        _apiMock
            .Setup(a => a.GetRatesOfExchangeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(apiException);

        var ex = Assert.ThrowsAsync<ExchangeRateProviderException>(() =>
            _sut.GetLatestRateOnOrBeforeAsync("X-Y", new DateOnly(2024, 6, 30), new DateOnly(2023, 12, 30)));

        Assert.That(ex!.InnerException, Is.SameAs(apiException));
    }

    [Test]
    public void GetLatestRateOnOrBeforeAsync_WrapsHttpRequestException()
    {
        var inner = new HttpRequestException("network down");
        _apiMock
            .Setup(a => a.GetRatesOfExchangeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(inner);

        var ex = Assert.ThrowsAsync<ExchangeRateProviderException>(() =>
            _sut.GetLatestRateOnOrBeforeAsync("X-Y", new DateOnly(2024, 6, 30), new DateOnly(2023, 12, 30)));

        Assert.That(ex!.InnerException, Is.SameAs(inner));
    }

    [Test]
    public void GetLatestRateOnOrBeforeAsync_WrapsTimeout()
    {
        _apiMock
            .Setup(a => a.GetRatesOfExchangeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("timeout", new TimeoutException()));

        var ex = Assert.ThrowsAsync<ExchangeRateProviderException>(() =>
            _sut.GetLatestRateOnOrBeforeAsync("X-Y", new DateOnly(2024, 6, 30), new DateOnly(2023, 12, 30)));

        Assert.That(ex!.Message, Does.Contain("timed out"));
    }

    [Test]
    public void GetLatestRateOnOrBeforeAsync_Throws_WhenCurrencyEmpty()
    {
        Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetLatestRateOnOrBeforeAsync("", new DateOnly(2024, 6, 30), new DateOnly(2023, 12, 30)));
    }
}
