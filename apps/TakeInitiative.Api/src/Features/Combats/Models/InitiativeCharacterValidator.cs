using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public class InitiativeCharacterValidator : CharacterValidator<InitiativeCharacter>
{
    public InitiativeCharacterValidator(IDiceRoller roller) : base(roller)
    {
        RuleFor(x => x.PlayerId)
            .NotEmpty();
    }
}

