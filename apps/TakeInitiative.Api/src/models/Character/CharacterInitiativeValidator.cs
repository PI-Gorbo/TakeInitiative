using FluentValidation;

namespace TakeInitiative.Api.Features;

public class CharacterInitiativeValidator : AbstractValidator<CharacterInitiative>
{
    public CharacterInitiativeValidator()
    {
        RuleFor(x => x.Fixed)
            .NotEmpty()
            .When(x => x.Strategy == InitiativeStrategy.Fixed)
            .WithMessage("Must provide for 'Fixed' value when the strategy is 'Fixed'.");

        RuleFor(x => x.Roll)
            .NotEmpty()
            .When(x => x.Strategy == InitiativeStrategy.Roll)
            .WithMessage("Must provide for 'Roll' value when the strategy is 'Roll'.");
    }
}
