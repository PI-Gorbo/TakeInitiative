using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class PutUpdateInitiativeCharacterRequestValidator : Validator<PutUpsertStagedCharacterRequest>
{
	public PutUpdateInitiativeCharacterRequestValidator()
	{
		RuleFor(x => x.CombatId)
			.NotEmpty();

		RuleFor(x => x.Character)
			.NotEmpty();
    }
}

