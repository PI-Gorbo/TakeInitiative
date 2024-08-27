using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class PostStagePlannedCharacters(IHubContext<CombatHub> hubContext) : Endpoint<PostStagePlannedCharactersRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/combat/stage/planned-character");
    }

    public override async Task HandleAsync(PostStagePlannedCharactersRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        Result<Combat> result = await new StagePlannedCharactersCommand()
        {
            CombatId = req.CombatId,
            UserId = userId,
            PlannedCharactersToStage = req.PlannedCharactersToStage
        }.ExecuteAsync();

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }


        await SendAsync(new() { Combat = result.Value });
        await hubContext.NotifyCombatUpdated(result.Value);
    }
}