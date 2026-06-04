using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Wex.Payments.Core.Exceptions;
using Wex.Payments.Core.Models;

namespace Wex.Payments.IntegrationTests;

[TestFixture]
public sealed class PurchaseEndpointTests
{
    private const string PurchasesUrl = "/api/v1/purchases";

    private WexPaymentsApiFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new WexPaymentsApiFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task StoreThenRetrieveConverted_FullFlow_Returns200WithAllFields()
    {
        _factory.FakeProvider.Handler = (_, _, _) =>
            Task.FromResult<ExchangeRate?>(new ExchangeRate("Brazil-Real", 5.165m, new DateOnly(2024, 3, 31)));

        // Requirement #1 — store.
        var storeResponse = await _client.PostAsJsonAsync(PurchasesUrl, new
        {
            description = "Laptop",
            transactionDate = "2024-06-30",
            amountUsd = 100.00m,
        });

        Assert.That(storeResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created), await storeResponse.Content.ReadAsStringAsync());

        using var storeDoc = JsonDocument.Parse(await storeResponse.Content.ReadAsStringAsync());
        var id = storeDoc.RootElement.GetProperty("id").GetGuid();
        Assert.That(id, Is.Not.EqualTo(Guid.Empty));

        // Requirement #2 — retrieve converted.
        var convertedResponse = await _client.GetAsync($"{PurchasesUrl}/{id}/converted?currency=Brazil-Real");
        Assert.That(convertedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), await convertedResponse.Content.ReadAsStringAsync());

        using var doc = JsonDocument.Parse(await convertedResponse.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("id").GetGuid(), Is.EqualTo(id));
            Assert.That(root.GetProperty("description").GetString(), Is.EqualTo("Laptop"));
            Assert.That(root.GetProperty("transactionDate").GetString(), Is.EqualTo("2024-06-30"));
            Assert.That(root.GetProperty("originalAmountUsd").GetDecimal(), Is.EqualTo(100.00m));
            Assert.That(root.GetProperty("countryCurrencyDesc").GetString(), Is.EqualTo("Brazil-Real"));
            Assert.That(root.GetProperty("exchangeRate").GetDecimal(), Is.EqualTo(5.165m));
            Assert.That(root.GetProperty("exchangeRateDate").GetString(), Is.EqualTo("2024-03-31"));
            Assert.That(root.GetProperty("convertedAmount").GetDecimal(), Is.EqualTo(516.50m));
        });
    }

    [Test]
    public async Task GetPurchase_ReturnsStoredFields()
    {
        var storeResponse = await _client.PostAsJsonAsync(PurchasesUrl, new
        {
            description = "Coffee",
            transactionDate = "2024-01-15",
            amountUsd = 9.99m,
        });
        using var storeDoc = JsonDocument.Parse(await storeResponse.Content.ReadAsStringAsync());
        var id = storeDoc.RootElement.GetProperty("id").GetGuid();

        var getResponse = await _client.GetAsync($"{PurchasesUrl}/{id}");

        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var doc = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(doc.RootElement.GetProperty("description").GetString(), Is.EqualTo("Coffee"));
            Assert.That(doc.RootElement.GetProperty("amountUsd").GetDecimal(), Is.EqualTo(9.99m));
        });
    }

    [Test]
    public async Task StorePurchase_Returns400_WhenDescriptionTooLong()
    {
        var response = await _client.PostAsJsonAsync(PurchasesUrl, new
        {
            description = new string('a', 51),
            transactionDate = "2024-06-30",
            amountUsd = 1.00m,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetConverted_Returns404_WhenPurchaseMissing()
    {
        var response = await _client.GetAsync($"{PurchasesUrl}/{Guid.NewGuid()}/converted?currency=Brazil-Real");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }

    [Test]
    public async Task GetConverted_Returns422_WhenNoRateAvailable()
    {
        _factory.FakeProvider.Handler = (_, _, _) => Task.FromResult<ExchangeRate?>(null);

        var id = await StoreSampleAsync();

        var response = await _client.GetAsync($"{PurchasesUrl}/{id}/converted?currency=Atlantis-Crystal");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("title").GetString(), Is.EqualTo("Purchase cannot be converted"));
    }

    [Test]
    public async Task GetConverted_Returns502_WhenProviderFails()
    {
        _factory.FakeProvider.Handler = (_, _, _) => throw new ExchangeRateProviderException("treasury down");

        var id = await StoreSampleAsync();

        var response = await _client.GetAsync($"{PurchasesUrl}/{id}/converted?currency=Brazil-Real");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
    }

    [Test]
    public async Task GetConverted_Returns400_WhenCurrencyMissing()
    {
        var id = await StoreSampleAsync();

        var response = await _client.GetAsync($"{PurchasesUrl}/{id}/converted");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/health");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private async Task<Guid> StoreSampleAsync()
    {
        var response = await _client.PostAsJsonAsync(PurchasesUrl, new
        {
            description = "Sample",
            transactionDate = "2024-06-30",
            amountUsd = 100.00m,
        });
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }
}
