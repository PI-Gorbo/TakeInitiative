using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class PutUpsertStagedCharacterRequestValidator : Validator<PutUpsertStagedCharacterRequest>
{
	public PutUpsertStagedCharacterRequestValidator()
	{

		RuleFor(x => x.CombatId)
			.NotEmpty();

		RuleFor(x => x.Character)
			.NotEmpty()
			.SetValidator(new StagedCombatCharacterDtoValidator());
	}
}

