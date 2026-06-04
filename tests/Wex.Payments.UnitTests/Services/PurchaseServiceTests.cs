using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wex.Payments.Core.Abstractions;
using Wex.Payments.Core.Exceptions;
using Wex.Payments.Core.Models;
using Wex.Payments.Core.Services;

namespace Wex.Payments.UnitTests.Services;

[TestFixture]
public sealed class PurchaseServiceTests
{
    private Mock<IPurchaseTransactionRepository> _repoMock = null!;
    private Mock<IExchangeRateProvider> _providerMock = null!;
    private PurchaseService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repoMock = new Mock<IPurchaseTransactionRepository>(MockBehavior.Strict);
        _providerMock = new Mock<IExchangeRateProvider>(MockBehavior.Strict);
        _sut = new PurchaseService(_repoMock.Object, _providerMock.Object, NullLogger<PurchaseService>.Instance);
    }

    [Test]
    public async Task StoreAsync_AssignsId_TrimsDescription_AndPersists()
    {
        PurchaseTransaction? captured = null;
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<PurchaseTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<PurchaseTransaction, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        var command = new StorePurchaseCommand("  Coffee beans  ", new DateOnly(2024, 6, 30), 12.34m);

        var stored = await _sut.StoreAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(stored.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(stored.Description, Is.EqualTo("Coffee beans"));
            Assert.That(stored.AmountUsd, Is.EqualTo(12.34m));
            Assert.That(captured, Is.SameAs(stored));
        });
    }

    [Test]
    public async Task StoreAsync_RoundsAmount_ToCent_AwayFromZero()
    {
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<PurchaseTransaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var stored = await _sut.StoreAsync(new StorePurchaseCommand("x", new DateOnly(2024, 1, 1), 1.005m));

        Assert.That(stored.AmountUsd, Is.EqualTo(1.01m));
    }

    [Test]
    public void GetAsync_Throws_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseTransaction?)null);

        var ex = Assert.ThrowsAsync<PurchaseTransactionNotFoundException>(() => _sut.GetAsync(id));
        Assert.That(ex!.Id, Is.EqualTo(id));
    }

    [Test]
    public async Task GetConvertedAsync_ReturnsAllFields_AndCorrectConvertedAmount()
    {
        var id = Guid.NewGuid();
        var txDate = new DateOnly(2024, 6, 30);
        var purchase = new PurchaseTransaction(id, "Laptop", txDate, 100.00m);

        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);
        _providerMock
            .Setup(p => p.GetLatestRateOnOrBeforeAsync("Brazil-Real", txDate, txDate.AddMonths(-6), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate("Brazil-Real", 5.165m, new DateOnly(2024, 3, 31)));

        var result = await _sut.GetConvertedAsync(id, "Brazil-Real");

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(id));
            Assert.That(result.Description, Is.EqualTo("Laptop"));
            Assert.That(result.TransactionDate, Is.EqualTo(txDate));
            Assert.That(result.OriginalAmountUsd, Is.EqualTo(100.00m));
            Assert.That(result.CountryCurrencyDesc, Is.EqualTo("Brazil-Real"));
            Assert.That(result.ExchangeRate, Is.EqualTo(5.165m));
            Assert.That(result.ExchangeRateDate, Is.EqualTo(new DateOnly(2024, 3, 31)));
            // 100.00 * 5.165 = 516.50
            Assert.That(result.ConvertedAmount, Is.EqualTo(516.50m));
        });
    }

    [Test]
    public async Task GetConvertedAsync_RoundsConvertedAmount_AwayFromZero()
    {
        var id = Guid.NewGuid();
        var purchase = new PurchaseTransaction(id, "x", new DateOnly(2024, 1, 1), 2.50m);

        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);
        _providerMock
            .Setup(p => p.GetLatestRateOnOrBeforeAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate("X-Y", 1.001m, new DateOnly(2023, 12, 31)));

        var result = await _sut.GetConvertedAsync(id, "X-Y");

        // 2.50 * 1.001 = 2.5025 -> 2.50 (nearest cent)
        Assert.That(result.ConvertedAmount, Is.EqualTo(2.50m));
    }

    [Test]
    public void GetConvertedAsync_Throws422Business_WhenNoRateInWindow()
    {
        var id = Guid.NewGuid();
        var txDate = new DateOnly(2024, 6, 30);
        var purchase = new PurchaseTransaction(id, "x", txDate, 100m);

        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);
        _providerMock
            .Setup(p => p.GetLatestRateOnOrBeforeAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);

        var ex = Assert.ThrowsAsync<ExchangeRateNotFoundException>(() => _sut.GetConvertedAsync(id, "Atlantis-Crystal"));
        Assert.Multiple(() =>
        {
            Assert.That(ex!.CountryCurrencyDesc, Is.EqualTo("Atlantis-Crystal"));
            Assert.That(ex.TransactionDate, Is.EqualTo(txDate));
            Assert.That(ex.Message, Does.Contain("cannot be converted"));
        });
    }

    [Test]
    public void GetConvertedAsync_Throws_WhenPurchaseMissing()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((PurchaseTransaction?)null);

        Assert.ThrowsAsync<PurchaseTransactionNotFoundException>(() => _sut.GetConvertedAsync(id, "Brazil-Real"));
    }

    [Test]
    public void GetConvertedAsync_BubblesProviderException()
    {
        var id = Guid.NewGuid();
        var purchase = new PurchaseTransaction(id, "x", new DateOnly(2024, 6, 30), 100m);

        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);
        _providerMock
            .Setup(p => p.GetLatestRateOnOrBeforeAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExchangeRateProviderException("treasury down"));

        var ex = Assert.ThrowsAsync<ExchangeRateProviderException>(() => _sut.GetConvertedAsync(id, "Brazil-Real"));
        Assert.That(ex!.Message, Is.EqualTo("treasury down"));
    }

    [Test]
    public void GetConvertedAsync_Throws_WhenCurrencyEmpty()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _sut.GetConvertedAsync(Guid.NewGuid(), "  "));
    }

    [Test]
    public void StoreAsync_Throws_WhenCommandNull()
    {
        Assert.ThrowsAsync<ArgumentNullException>(() => _sut.StoreAsync(null!));
    }
}
