using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features;

public class PlayerCharacterDTOValidator : AbstractValidator<PlayerCharacterDTO>
{
    public PlayerCharacterDTOValidator(IDiceRoller roller)
    {
        RuleFor(x => x.Name)
            .NotEmpty();

        RuleFor(x => x.Health)
            .NotNull();

        RuleFor(x => x.Initiative)
            .SetValidator(new UnevaluatedCharacterInitiativeValidator(roller));
    }
}

