using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

public class PutUpsertStagedCharacterRequestValidator : Validator<PutUpsertStagedCharacterRequest>
{
    public PutUpsertStagedCharacterRequestValidator()
    {

        RuleFor(x => x.CombatId)
            .NotEmpty();

        RuleFor(x => x.Character)
            .NotEmpty()
            .SetValidator(new StagedCombatCharacterDtoValidator());
    }
}

