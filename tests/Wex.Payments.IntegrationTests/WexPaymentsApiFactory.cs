using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wex.Payments.Core.Abstractions;
using Wex.Payments.IntegrationTests.Fakes;

namespace Wex.Payments.IntegrationTests;

internal sealed class WexPaymentsApiFactory : WebApplicationFactory<Program>
{
    public FakeExchangeRateProvider FakeProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Treasury:BaseUrl"] = "https://test.invalid/",
                ["Treasury:TimeoutSeconds"] = "5",
                ["Treasury:RetryCount"] = "0",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IExchangeRateProvider>();
            services.AddSingleton<IExchangeRateProvider>(FakeProvider);
        });
    }
}
