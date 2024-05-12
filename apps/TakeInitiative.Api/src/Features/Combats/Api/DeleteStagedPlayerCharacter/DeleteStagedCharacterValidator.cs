using FluentValidation;

namespace TakeInitiative.Api.Features;

public class DeleteStagedCharacterValidator : AbstractValidator<DeleteStagedCharacterRequest>
{
    public DeleteStagedCharacterValidator()
    {

        RuleFor(x => x.CharacterId)
            .NotEmpty();
    }
}
