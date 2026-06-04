using Microsoft.Extensions.DependencyInjection;
using Wex.Payments.Core.Abstractions;
using Wex.Payments.Core.Persistence;
using Wex.Payments.Core.Services;

namespace Wex.Payments.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddWexPaymentsCore(this IServiceCollection services)
    {
        // Singleton so stored purchases persist for the lifetime of the process (no external DB).
        services.AddSingleton<IPurchaseTransactionRepository, InMemoryPurchaseTransactionRepository>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        return services;
    }
}
