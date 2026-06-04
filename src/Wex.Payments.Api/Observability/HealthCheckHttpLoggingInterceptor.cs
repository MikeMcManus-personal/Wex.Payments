using Microsoft.AspNetCore.HttpLogging;

namespace Wex.Payments.Api.Observability;

/// <summary>
/// Suppresses HTTP access logging for high-frequency, low-signal endpoints — the
/// <c>/health</c> liveness probe and the Swagger UI — so the access log stays focused on
/// real API traffic. Every other request is logged normally.
/// </summary>
internal sealed class HealthCheckHttpLoggingInterceptor : IHttpLoggingInterceptor
{
    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
    {
        var path = logContext.HttpContext.Request.Path;

        if (path.StartsWithSegments("/health")
            || path.StartsWithSegments("/swagger")
            || path == "/"
            || path == "/index.html")
        {
            logContext.LoggingFields = HttpLoggingFields.None;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext) =>
        ValueTask.CompletedTask;
}
