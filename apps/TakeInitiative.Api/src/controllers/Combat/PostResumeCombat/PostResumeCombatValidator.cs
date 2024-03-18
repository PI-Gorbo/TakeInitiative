using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class PostResumeCombatValidator : Validator<PostResumeCombatRequest>
{
	public PostResumeCombatValidator()
	{
		RuleFor(x => x.CombatId)
			.NotEmpty();
	}
}
