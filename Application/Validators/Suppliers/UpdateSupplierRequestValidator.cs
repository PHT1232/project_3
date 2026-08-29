using Application.DTOs.Suppliers;
using FluentValidation;

namespace Application.Validators.Suppliers;

public class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
{
    public UpdateSupplierRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LeadTimeDays).GreaterThanOrEqualTo(0);
    }
}
