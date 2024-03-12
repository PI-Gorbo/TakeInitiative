using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class DeleteStagedCharacterValidator : AbstractValidator<DeleteStagedCharacterRequest>
{
	public DeleteStagedCharacterValidator()
	{

		RuleFor(x => x.CharacterId)
			.NotEmpty();
	}
}
