using FluentValidation;

namespace TakeInitiative.Api.Models;

public class CharacterValidator<TCharacter> : AbstractValidator<TCharacter>
	where TCharacter : Character
{
	protected CharacterValidator()
	{
		RuleFor(x => x.Id).NotEmpty();

		RuleFor(x => x.Name)
			.NotEmpty();
			
		RuleFor(x => x.Health!.CurrentHealth)
			.NotEmpty()
			.When(x => x.Health != null);
			
		RuleFor(x => x.Initiative)
			.NotEmpty()
			.SetValidator(new CharacterInitiativeValidator());
	}
}
