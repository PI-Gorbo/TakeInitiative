using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

public class PutStagePlannedCharacterRequestValidator : Validator<PutStagePlannedCharactersRequest>
{
    public PutStagePlannedCharacterRequestValidator()
    {
        RuleFor(x => x.CombatId).NotEmpty();
        RuleFor(x => x.PlannedCharactersToStage)
            .NotEmpty();
    }
}
