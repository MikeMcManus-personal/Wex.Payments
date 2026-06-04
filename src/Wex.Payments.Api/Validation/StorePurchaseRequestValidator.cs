using FluentValidation;
using Wex.Payments.Api.Contracts;

namespace Wex.Payments.Api.Validation;

public sealed class StorePurchaseRequestValidator : AbstractValidator<StorePurchaseRequest>
{
    // Lower bound to reject obviously garbage / unset dates. The brief only requires a
    // valid date format (the JSON binder enforces that); we intentionally do NOT reject
    // future dates here — a future purchase simply fails conversion (no rate in window).
    private static readonly DateOnly MinTransactionDate = new(1900, 1, 1);

    public StorePurchaseRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("description is required.")
            // Validate the trimmed length so it matches what the service stores after trimming
            // (a 50-char description padded with whitespace is accepted, not rejected at 52).
            .Must(d => d is null || d.Trim().Length <= 50)
            .WithMessage("description must not exceed 50 characters.");

        RuleFor(x => x.AmountUsd)
            .GreaterThan(0m).WithMessage("amountUsd must be a positive amount.")
            .Must(HaveAtMostTwoDecimals).WithMessage("amountUsd must be rounded to the nearest cent (at most 2 decimal places).");

        RuleFor(x => x.TransactionDate)
            .GreaterThanOrEqualTo(MinTransactionDate)
            .WithMessage("transactionDate must be a valid date.");
    }

    private static bool HaveAtMostTwoDecimals(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero) == value;
}
