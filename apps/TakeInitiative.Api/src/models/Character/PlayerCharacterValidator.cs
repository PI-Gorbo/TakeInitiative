using FluentValidation;

namespace TakeInitiative.Api.Models;

public class PlayerCharacterValidator : TCharacterValidator<PlayerCharacter>
{
	public PlayerCharacterValidator()
	{
		RuleFor(x => x.Id).NotEmpty();
		RuleFor(x => x.PlayerId).NotEmpty();
	}
}

