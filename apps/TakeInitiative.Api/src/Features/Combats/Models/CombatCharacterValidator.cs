using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

public class CombatCharacterValidator : CharacterValidator<CombatCharacter>
{
    public CombatCharacterValidator()
    {
        RuleFor(x => x.PlayerId)
            .NotEmpty();
    }
}

