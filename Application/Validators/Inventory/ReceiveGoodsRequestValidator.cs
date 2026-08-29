using Application.DTOs.Inventory;
using FluentValidation;

namespace Application.Validators.Inventory;

public class ReceiveGoodsRequestValidator : AbstractValidator<ReceiveGoodsRequest>
{
    public ReceiveGoodsRequestValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
