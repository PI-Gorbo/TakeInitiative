using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features;

public class PutPlannedCombatStageRequestValidator : Validator<PutPlannedCombatStageRequest>
{
    public PutPlannedCombatStageRequestValidator()
    {
        RuleFor(x => x.CombatId).NotEmpty();
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
    }
}
