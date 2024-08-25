using CSharpFunctionalExtensions;
using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features;

public class CharacterInitiativeValidator : AbstractValidator<CharacterInitiative>
{
    public CharacterInitiativeValidator(IDiceRoller diceRoller)
    {
        RuleFor(x => x.Fixed)
            .NotEmpty()
            .When(x => x.Strategy == InitiativeStrategy.Fixed)
            .WithMessage("Must provide for 'Fixed' value when the strategy is 'Fixed'.");

        RuleFor(x => x.Roll)
            .NotEmpty()
            .When(x => x.Strategy == InitiativeStrategy.Roll)
            .WithMessage("Must provide for 'Roll' value when the strategy is 'Roll'.");

        When(x => x.Strategy == InitiativeStrategy.Roll, () =>
        {
            RuleFor(x => x.Value)
                .Custom((value, context) => 
                    diceRoller
                        .EvaluateRoll(value)
                        .TapError(context.AddFailure)
            );
        });

        When(x => x.Strategy == InitiativeStrategy.Fixed, () => {
            RuleFor(x => x.Value)
                .Must(x =>
                {
                    try {
                        var result =  Convert.ToInt32(x);
                        return true;
                    } catch {
                        return false;
                    }
                }).WithMessage("Fixed initiative must be an integer.");
        });
    }
}
