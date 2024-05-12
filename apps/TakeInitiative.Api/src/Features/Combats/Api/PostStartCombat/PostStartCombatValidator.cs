using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features;

public class PostStartCombatValidator : Validator<PostStartCombatRequest>
{
    public PostStartCombatValidator()
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();
    }
}
