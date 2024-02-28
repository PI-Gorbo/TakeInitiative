using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class DeletePlannedCombatNpcRequestValidator : Validator<DeletePlannedCombatNpcRequest>
{
	public DeletePlannedCombatNpcRequestValidator()
	{
		RuleFor(x => x.CombatId)
			.NotEmpty();

		RuleFor(x => x.StageId)
			.NotEmpty();

		RuleFor(x => x.NpcId)
			.NotEmpty();

	}
}
