namespace Application.Validators.Requests;

using Application.DTOs.Requests;
using FluentValidation;

/// <summary>
/// Validates <see cref="CreateRequestCommand"/> — a requestor's new request submission
/// (approval_transaction.drawio: "Nhập request xong nhấn 'Gữi'").
///
/// - Items list must not be empty and must be distinct by ItemId.
/// - Each item quantity must be > 0.
/// - RequiredByDate, if provided, does not need validation (can be null or any date).
/// </summary>
public class CreateRequestCommandValidator : AbstractValidator<CreateRequestCommand>
{
    public CreateRequestCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Request must have at least one item.");

        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.ItemId).Distinct().Count() == items.Count)
            .WithMessage("Duplicate items are not allowed.");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.ItemId)
                    .GreaterThan(0)
                    .WithMessage("ItemId must be positive.");

                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0)
                    .WithMessage("Quantity must be greater than zero.");

                item.RuleFor(i => i.Quantity)
                    .LessThanOrEqualTo(9999)
                    .WithMessage("Quantity cannot exceed 9999.");
            });
    }
}
