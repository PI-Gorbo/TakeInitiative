using FluentValidation;

namespace TakeInitiative.Api.Models;

public class PlannedCombatNpcValidator : TCharacterValidator<PlannedCombatNpc>
{
	public PlannedCombatNpcValidator()
	{
		RuleFor(x => x.Quantity)
			.Must(quantity => quantity >= 1)
			.WithMessage("Quantity must be equal to or greater than 1.");
	}
}
