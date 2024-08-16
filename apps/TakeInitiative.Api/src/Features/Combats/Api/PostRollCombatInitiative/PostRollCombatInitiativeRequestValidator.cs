using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

public class PostRollCombatInitiativeRequestValidator : Validator<PostRollCombatInitiativeRequest>
{
    public PostRollCombatInitiativeRequestValidator()
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();
    }
}
