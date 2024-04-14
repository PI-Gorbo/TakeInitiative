using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class PutPlannedCombatStageRequestValidator : Validator<PutPlannedCombatStageRequest>
{
    public PutPlannedCombatStageRequestValidator()
    {
        RuleFor(x => x.CombatId).NotEmpty();
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
    }
}
