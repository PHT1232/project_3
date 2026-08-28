using Application.DTOs.SupplierRequests;
using FluentValidation;

namespace Application.Validators.SupplierRequests;

/// <summary>
/// Shape-level validation only. Whether an item exists, is active, or resolves to a real supplier
/// needs the database, so those checks live in SupplierRequestService.
/// </summary>
public class CreateSupplierRequestCommandValidator : AbstractValidator<CreateSupplierRequestCommand>
{
    public CreateSupplierRequestCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Select at least one item before submitting a supplier request.");

        RuleForEach(x => x.Items).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity)
                .GreaterThan(0)
                .WithMessage("Requested quantity must be greater than zero.");
        });

        // Two lines for the same item would create an ambiguous order (and violate the unique
        // index on SupplierRequestItems); the client is expected to merge them in the cart.
        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.ItemId).Distinct().Count() == items.Count)
            .WithMessage("Each item may appear only once in a supplier request.")
            .When(x => x.Items is { Count: > 0 });
    }
}
