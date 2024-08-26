using FastEndpoints;
using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public class PutUpsertStagedCharacterRequestValidator : Validator<PutUpsertStagedCharacterRequest>
{
    public PutUpsertStagedCharacterRequestValidator(IDiceRoller roller)
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();

        RuleFor(x => x.Character)
            .NotEmpty()
            .SetValidator(new StagedCombatCharacterDtoValidator(roller));
    }
}

