using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features;

public class CharacterValidator<TCharacter> : AbstractValidator<TCharacter>
    where TCharacter : Character
{
    protected CharacterValidator(IDiceRoller diceRoller)
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty();

        RuleFor(x => x.Health)
            .NotNull();

        RuleFor(x => x.Initiative)
            .NotEmpty()
            .SetValidator(new UnevaluatedCharacterInitiativeValidator(diceRoller));

        RuleFor(x => x.Health)
            .SetValidator(new UnevaluatedCharacterHealthValidator(diceRoller));
    }
}
