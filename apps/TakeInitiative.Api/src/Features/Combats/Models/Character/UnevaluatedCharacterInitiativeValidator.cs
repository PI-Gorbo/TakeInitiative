using CSharpFunctionalExtensions;
using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features;

public class UnevaluatedCharacterInitiativeValidator : AbstractValidator<UnevaluatedCharacterInitiative>
{
    public UnevaluatedCharacterInitiativeValidator(IDiceRoller diceRoller)
    {
        RuleFor(x => x.Roll)
            .Custom((value, context) =>
                diceRoller
                    .Check(value)
                    .TapError(context.AddFailure)
        );
    }
}
