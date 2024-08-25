using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public class CombatCharacterValidator : CharacterValidator<InitiativeCharacter>
{
    public CombatCharacterValidator(IDiceRoller roller) : base(roller)
    {
        RuleFor(x => x.PlayerId)
            .NotEmpty();
    }
}

