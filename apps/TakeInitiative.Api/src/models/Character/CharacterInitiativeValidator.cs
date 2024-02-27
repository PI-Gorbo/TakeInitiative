using FluentValidation;

namespace TakeInitiative.Api.Models;

public class CharacterInitiativeValidator : AbstractValidator<CharacterInitiative>
{
	public CharacterInitiativeValidator()
	{
		RuleFor(x => x.Fixed)
			.NotEmpty()
			.When(x => x.Strategy == InitiativeStrategy.Fixed);
		
		RuleFor(x => x.Roll)
			.NotEmpty()
			.When(x => x.Strategy == InitiativeStrategy.Roll);
	}
}
