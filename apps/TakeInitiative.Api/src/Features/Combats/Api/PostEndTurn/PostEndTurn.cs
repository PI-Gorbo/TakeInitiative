using System.Net;
using FastEndpoints;
using TakeInitiative.Utilities.Extensions;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Features.Combats;

public class PostEndTurn(IHubContext<CombatHub> hubContext) : Endpoint<PostEndTurnRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/combat/turn/end");
    }

    public override async Task HandleAsync(PostEndTurnRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        Result<Combat> result = await new EndTurnCommand()
        {
            CombatId = req.CombatId,
            UserId = userId
        }.ExecuteAsync(ct);

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(new()
        {
            Combat = result.Value
        });
        await hubContext.NotifyCombatUpdated(result.Value);
    }
}