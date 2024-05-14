using FluentValidation;

namespace TakeInitiative.Api.Features;

public class PlayerCharacterValidator : CharacterValidator<PlayerCharacter>
{
    public PlayerCharacterValidator()
    {
        RuleFor(x => x.PlayerId).NotEmpty();
    }
}

