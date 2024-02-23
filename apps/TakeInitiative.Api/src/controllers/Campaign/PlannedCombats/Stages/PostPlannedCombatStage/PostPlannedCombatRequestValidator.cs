using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class PostPlannedCombatRequestValidator : Validator<PostPlannedCombatStageRequest>
{
    public PostPlannedCombatRequestValidator()
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty();
    }
}
