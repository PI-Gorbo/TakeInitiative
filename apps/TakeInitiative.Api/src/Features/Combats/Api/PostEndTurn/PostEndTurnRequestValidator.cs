using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

public class PostEndTurnRequestValidator : Validator<PostEndTurnRequest>
{
    public PostEndTurnRequestValidator()
    {
        RuleFor(x => x.CombatId).NotEmpty();
    }
}
