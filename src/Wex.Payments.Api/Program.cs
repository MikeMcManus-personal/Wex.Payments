using FluentValidation;
using Microsoft.AspNetCore.HttpLogging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Wex.Payments.Api.Endpoints;
using Wex.Payments.Api.Middleware;
using Wex.Payments.Api.Observability;
using Wex.Payments.Api.Validation;
using Wex.Payments.Core.DependencyInjection;
using Wex.Payments.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Wex.Payments Purchase Conversion API", Version = "v1" });
});

builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields =
        HttpLoggingFields.RequestMethod |
        HttpLoggingFields.RequestPath |
        HttpLoggingFields.RequestQuery |
        HttpLoggingFields.ResponseStatusCode |
        HttpLoggingFields.Duration;
    // One combined entry per request instead of separate request/response lines.
    o.CombineLogs = true;
});

// Keep the access log focused on real API traffic: skip the /health probe and Swagger UI.
builder.Services.AddHttpLoggingInterceptor<HealthCheckHttpLoggingInterceptor>();

builder.Services.AddValidatorsFromAssemblyContaining<StorePurchaseRequestValidator>();
builder.Services.AddScoped(typeof(ValidationEndpointFilter<>));

// Surface request-body binding failures (e.g. an unparseable transactionDate) through the
// exception middleware so they return a uniform problem+json 400 like every other error.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

builder.Services.AddWexPaymentsCore();
builder.Services.AddWexPaymentsInfrastructure(builder.Configuration);

// ---- Observability: OpenTelemetry traces, metrics, and logs ----
// Auto-instrument ASP.NET Core (incoming requests) and HttpClient (the Treasury call) so a
// request and its upstream call form a single distributed trace. Metrics cover the HTTP
// server and client, our exchange-rate cache, and the Polly resilience pipeline.
var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(builder.Environment.ApplicationName))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("Wex.Payments.ExchangeRateCache")
        .AddMeter("Polly"));

// Route ILogger output through OpenTelemetry too, carrying scopes (incl. the trace id).
builder.Logging.AddOpenTelemetry(o =>
{
    o.IncludeScopes = true;
    o.IncludeFormattedMessage = true;
});

// Exporters by environment: Development -> Console (zero dependencies); Testing -> none
// (the integration suite must stay offline and deterministic); otherwise -> OTLP, which
// honors the standard OTEL_EXPORTER_OTLP_ENDPOINT (defaults to http://localhost:4317).
if (builder.Environment.IsDevelopment())
{
    // Traces + metrics to the console (the plain console logger can't represent these).
    // Logs are intentionally NOT re-exported here: the console logger already prints them
    // (with trace-id scopes), so this avoids duplicate log lines locally. Logs still flow
    // through the OpenTelemetry pipeline to OTLP in other environments.
    otel.WithTracing(t => t.AddConsoleExporter());
    otel.WithMetrics(m => m.AddConsoleExporter());
}
else if (!builder.Environment.IsEnvironment("Testing"))
{
    otel.WithTracing(t => t.AddOtlpExporter());
    otel.WithMetrics(m => m.AddOtlpExporter());
    builder.Logging.AddOpenTelemetry(o => o.AddOtlpExporter());
}

var app = builder.Build();

// Swagger UI at the root for an easy, dependency-free way to exercise the API.
// Disabled in Production so the API surface isn't exposed there.
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Wex.Payments Purchase Conversion API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapPurchaseEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");

app.Run();

public partial class Program;
