using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class PutUpsertStagedPlayerCharacterRequestValidator : AbstractValidator<PutUpsertStagedPlayerCharacterRequest>
{
	public PutUpsertStagedPlayerCharacterRequestValidator()
	{

		RuleFor(x => x.CombatId)
			.NotEmpty();

		RuleFor(x => x.Character)
			.NotEmpty()
			.SetValidator(new StagedCombatCharacterDtoValidator());
	}
}

