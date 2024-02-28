using FluentValidation;

namespace TakeInitiative.Api.Models;

public abstract class TCharacterValidator<TCharacter> : AbstractValidator<TCharacter>
	where TCharacter : ICharacter
{
	protected TCharacterValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty();
			
		RuleFor(x => x.Health.CurrentHealth)
			.NotEmpty()
			.When(x => x.Health != null);
			
		RuleFor(x => x.Initiative)
			.NotEmpty()
			.SetValidator(new CharacterInitiativeValidator());
	}
}
