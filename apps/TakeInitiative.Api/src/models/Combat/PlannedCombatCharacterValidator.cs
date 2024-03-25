using FluentValidation;

namespace TakeInitiative.Api.Models;

public class PlannedCombatCharacterValidator : CharacterValidator<PlannedCombatCharacter>
{
	public PlannedCombatCharacterValidator()
	{
		RuleFor(x => x.Quantity)
			.Must(quantity => quantity >= 1)
			.WithMessage("Quantity must be equal to or greater than 1.");
	}
}
