using Wex.Payments.Core.Models;
using Wex.Payments.Core.Persistence;

namespace Wex.Payments.UnitTests.Persistence;

[TestFixture]
public sealed class InMemoryPurchaseTransactionRepositoryTests
{
    [Test]
    public async Task AddThenGet_ReturnsSameTransaction()
    {
        var repo = new InMemoryPurchaseTransactionRepository();
        var purchase = new PurchaseTransaction(Guid.NewGuid(), "x", new DateOnly(2024, 1, 1), 10m);

        await repo.AddAsync(purchase);
        var fetched = await repo.GetByIdAsync(purchase.Id);

        Assert.That(fetched, Is.SameAs(purchase));
    }

    [Test]
    public async Task GetById_ReturnsNull_WhenMissing()
    {
        var repo = new InMemoryPurchaseTransactionRepository();

        var fetched = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.That(fetched, Is.Null);
    }

    [Test]
    public async Task Add_Throws_OnDuplicateId()
    {
        var repo = new InMemoryPurchaseTransactionRepository();
        var id = Guid.NewGuid();
        await repo.AddAsync(new PurchaseTransaction(id, "x", new DateOnly(2024, 1, 1), 10m));

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.AddAsync(new PurchaseTransaction(id, "y", new DateOnly(2024, 1, 2), 20m)));
    }
}
