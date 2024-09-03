using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

public class PostStagePlannedCharacterRequestValidator : Validator<PostStagePlannedCharactersRequest>
{
    public PostStagePlannedCharacterRequestValidator()
    {
        RuleFor(x => x.CombatId).NotEmpty();
        RuleFor(x => x.PlannedCharactersToStage)
            .NotEmpty();
    }
}
