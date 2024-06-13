using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record PostRollStagedCharactersIntoInitiativeRequest
{
    public required Guid CombatId { get; set; }
    public required Guid[] CharacterIds { get; set; }
}

public class PostRollStagedCharactersIntoInitiativeValidator : Validator<PostRollStagedCharactersIntoInitiativeRequest>
{
    public PostRollStagedCharactersIntoInitiativeValidator()
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();

        RuleFor(x => x.CharacterIds)
            .NotEmpty();
    }
}

public class PostRollStagedCharactersIntoInitiative(IHubContext<CombatHub> hubContext) : Endpoint<PostRollStagedCharactersIntoInitiativeRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/combat/stage/roll");
    }

    public override async Task HandleAsync(PostRollStagedCharactersIntoInitiativeRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        Result<Combat> result = await new RollStagedCharacterIntoInitiativeCommand()
        {
            CombatId = req.CombatId,
            UserId = userId,
            CharacterIds = req.CharacterIds
        }.ExecuteAsync();

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(new() { Combat = result.Value });
        await hubContext.NotifyCombatUpdated(result.Value);
    }
}