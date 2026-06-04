using FluentValidation;
using Microsoft.AspNetCore.HttpLogging;
using Wex.Payments.Api.Endpoints;
using Wex.Payments.Api.Middleware;
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
});

builder.Services.AddValidatorsFromAssemblyContaining<StorePurchaseRequestValidator>();
builder.Services.AddScoped(typeof(ValidationEndpointFilter<>));

builder.Services.AddWexPaymentsCore();
builder.Services.AddWexPaymentsInfrastructure(builder.Configuration);

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
