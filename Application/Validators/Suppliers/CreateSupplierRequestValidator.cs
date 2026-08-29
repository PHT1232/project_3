using Application.DTOs.Suppliers;
using FluentValidation;

namespace Application.Validators.Suppliers;

public class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
{
    public CreateSupplierRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LeadTimeDays).GreaterThanOrEqualTo(0);
    }
}
