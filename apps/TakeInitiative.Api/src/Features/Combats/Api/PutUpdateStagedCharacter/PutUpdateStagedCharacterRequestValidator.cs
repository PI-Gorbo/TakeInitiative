using FastEndpoints;
using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public class PutUpdateStagedCharacterRequestValidator : Validator<PutUpdateStagedCharacterRequest>
{
    public PutUpdateStagedCharacterRequestValidator(IDiceRoller roller)
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();

        RuleFor(x => x.Character)
            .NotEmpty()
            .SetValidator(new StagedCombatCharacterDtoValidator(roller));
    }
}

