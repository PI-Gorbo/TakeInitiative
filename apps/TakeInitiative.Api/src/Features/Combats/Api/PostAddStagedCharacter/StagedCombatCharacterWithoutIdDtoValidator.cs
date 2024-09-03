using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public class StagedCombatCharacterWithoutIdDtoValidator : AbstractValidator<StagedCombatCharacterWithoutIdDto>
{
    public StagedCombatCharacterWithoutIdDtoValidator(IDiceRoller diceRoller)
    {
        RuleFor(x => x.Name)
            .NotEmpty();

        RuleFor(x => x.Health)
            .NotNull();

        RuleFor(x => x.Initiative)
            .NotEmpty()
            .SetValidator(new UnevaluatedCharacterInitiativeValidator(diceRoller));
    }
}