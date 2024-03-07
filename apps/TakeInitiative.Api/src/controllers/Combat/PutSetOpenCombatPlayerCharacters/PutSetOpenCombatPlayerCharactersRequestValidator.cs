using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class PutSetOpenCombatPlayerCharactersRequestValidator : AbstractValidator<PutSetOpenCombatPlayerCharactersRequest>
{
	public PutSetOpenCombatPlayerCharactersRequestValidator()
	{
		RuleFor(x => x.CombatId)
			.NotEmpty();

		var validator = new CombatPlayerCharacterValidator();
		RuleForEach(x => x.Characters)
			.SetValidator(validator);
	}
}
