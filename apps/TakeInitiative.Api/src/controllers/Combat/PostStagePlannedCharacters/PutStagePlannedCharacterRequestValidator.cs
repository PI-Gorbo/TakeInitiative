using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class PutStagePlannedCharacterRequestValidator : Validator<PutStagePlannedCharactersRequest>
{
    public PutStagePlannedCharacterRequestValidator()
    {
        RuleFor(x => x.CombatId).NotEmpty();
        RuleFor(x => x.PlannedCharactersToStage)
            .NotEmpty();
    }
}
