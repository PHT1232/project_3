namespace Application.Validators.Support;

using Application.DTOs.Support;
using FluentValidation;

/// <summary>
/// Guards <see cref="CreateSupportMessageCommand"/>. Lengths double as a size bound on a
/// free-text field any authenticated user can write to.
/// </summary>
public class CreateSupportMessageCommandValidator : AbstractValidator<CreateSupportMessageCommand>
{
    public const int MaxSubjectLength = 200;
    public const int MaxBodyLength = 4000;
    public const int MaxAreaLength = 80;
    public const int MaxDiagnosticsLength = 4000;

    public CreateSupportMessageCommandValidator()
    {
        RuleFor(x => x.Area)
            .NotEmpty().WithMessage("Pick an area.")
            .MaximumLength(MaxAreaLength);

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Add a short subject.")
            .MaximumLength(MaxSubjectLength);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Describe the problem or question.")
            .MaximumLength(MaxBodyLength);

        RuleFor(x => x.Diagnostics)
            .MaximumLength(MaxDiagnosticsLength);
    }
}
