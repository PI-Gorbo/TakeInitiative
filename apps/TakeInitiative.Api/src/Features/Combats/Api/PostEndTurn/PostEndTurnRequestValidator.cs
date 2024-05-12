using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features;

public class PostEndTurnRequestValidator : Validator<PostEndTurnRequest>
{
    public PostEndTurnRequestValidator()
    {
        RuleFor(x => x.CombatId).NotEmpty();
    }
}
