using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

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
