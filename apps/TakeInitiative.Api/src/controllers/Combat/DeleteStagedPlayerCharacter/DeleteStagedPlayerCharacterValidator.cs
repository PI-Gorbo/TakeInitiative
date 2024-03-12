using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class DeleteStagedPlayerCharacterValidator : AbstractValidator<DeleteStagedPlayerCharacterRequest>
{
	public DeleteStagedPlayerCharacterValidator()
	{
		RuleFor(x => x.UserId)
			.NotEmpty();

		RuleFor(x => x.CharacterId)
			.NotEmpty();
	}
}
