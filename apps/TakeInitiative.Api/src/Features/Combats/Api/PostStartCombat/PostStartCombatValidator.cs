using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

public class PostStartCombatValidator : Validator<PostStartCombatRequest>
{
    public PostStartCombatValidator()
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();
    }
}
