namespace Application.Validators.Ai;

using Application.DTOs.Ai;
using FluentValidation;

/// <summary>
/// Guards <see cref="RequestAssistantCommand"/>. The length cap is a cost control
/// (Plan §5.2 rule 6) as much as a validation rule — it bounds the prompt size.
/// </summary>
public class RequestAssistantCommandValidator : AbstractValidator<RequestAssistantCommand>
{
    public const int MaxTextLength = 1000;

    public RequestAssistantCommandValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Describe what you need before asking the assistant.")
            .MaximumLength(MaxTextLength)
            .WithMessage($"Keep the description under {MaxTextLength} characters.");
    }
}
