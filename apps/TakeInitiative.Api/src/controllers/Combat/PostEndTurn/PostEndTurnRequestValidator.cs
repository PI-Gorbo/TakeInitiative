using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Controllers;

public class PostEndTurnRequestValidator : Validator<PostEndTurnRequest>
{
    public PostEndTurnRequestValidator()
    {
        RuleFor(x => x.CombatId).NotEmpty();
    }
}
