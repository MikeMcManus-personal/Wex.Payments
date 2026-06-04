using FluentValidation;

namespace Wex.Payments.Api.Validation;

public sealed class ValidationEndpointFilter<T> : IEndpointFilter where T : class
{
    private readonly IValidator<T> _validator;

    public ValidationEndpointFilter(IValidator<T> validator) => _validator = validator;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return Results.Problem(
                title: "Invalid request",
                detail: $"Expected request body of type {typeof(T).Name} was missing.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await _validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return Results.ValidationProblem(errors,
            title: "One or more validation errors occurred.",
            statusCode: StatusCodes.Status400BadRequest);
    }
}
