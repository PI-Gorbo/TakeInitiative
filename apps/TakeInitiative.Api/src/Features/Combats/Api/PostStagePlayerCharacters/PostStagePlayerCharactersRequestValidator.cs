using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

public class PostStagePlayerCharactersRequestValidator : Validator<PostStagePlayerCharactersRequest>
{
    public PostStagePlayerCharactersRequestValidator()
    {
        RuleFor(x => x.CombatId).NotEmpty();
        RuleFor(x => x.CharacterIds).NotEmpty();
    }
}
