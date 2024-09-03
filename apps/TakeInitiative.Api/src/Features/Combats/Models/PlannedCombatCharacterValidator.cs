using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public class PlannedCombatCharacterValidator : CharacterValidator<PlannedCharacter>
{
    public PlannedCombatCharacterValidator(IDiceRoller roller) : base(roller)
    {
        RuleFor(x => x.Quantity)
            .Must(quantity => quantity >= 1)
            .WithMessage("Quantity must be equal to or greater than 1.");
    }
}
