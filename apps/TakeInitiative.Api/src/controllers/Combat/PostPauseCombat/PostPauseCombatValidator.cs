using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class PostPauseCombatValidator : Validator<PostPauseCombatRequest>
{
	public PostPauseCombatValidator()
	{
		RuleFor(x => x.CombatId)
			.NotEmpty();
	}
}
