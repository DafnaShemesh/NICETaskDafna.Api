using FluentValidation;

namespace NICETaskDafna.Api.Contracts.Validation;

public class SuggestTaskRequestValidator : AbstractValidator<SuggestTaskRequest>
{
    public SuggestTaskRequestValidator()
    {
        RuleFor(x => x.Utterance)
            .NotEmpty().WithMessage("Utterance is required.")
            .MaximumLength(2000).WithMessage("Utterance is too long (max 2000).");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("SessionId is required.");

        RuleFor(x => x.Timestamp)
            .NotEqual(default(DateTime)).WithMessage("Timestamp must be a valid ISO-8601 UTC value.");
    }
}
