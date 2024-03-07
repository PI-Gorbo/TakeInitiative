using FluentValidation;

namespace TakeInitiative.Api.Models;

public class PlayerCharacterValidator : CharacterValidator<PlayerCharacter>
{
	public PlayerCharacterValidator()
	{
		RuleFor(x => x.PlayerId).NotEmpty();
	}
}

