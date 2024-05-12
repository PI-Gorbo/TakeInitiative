using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features;

public class PostFinishCombatValidator : Validator<PostFinishCombatRequest>
{
    public PostFinishCombatValidator()
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();
    }
}
