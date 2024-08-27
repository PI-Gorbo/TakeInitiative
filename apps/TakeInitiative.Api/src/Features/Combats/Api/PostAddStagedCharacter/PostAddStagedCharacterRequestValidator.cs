using FastEndpoints;
using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public class PostAddStagedCharacterRequestValidator : Validator<PostAddStagedCharacterRequest>
{
    public PostAddStagedCharacterRequestValidator(IDiceRoller roller)
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();

        RuleFor(x => x.Character)
            .NotEmpty()
            .SetValidator(new StagedCombatCharacterWithoutIdDtoValidator(roller));
    }
}



