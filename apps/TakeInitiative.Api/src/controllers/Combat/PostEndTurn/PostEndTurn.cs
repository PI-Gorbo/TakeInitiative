using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;
using CSharpFunctionalExtensions;
using TakeInitiative.Utilities;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Api.CQRS;

namespace TakeInitiative.Api.Controllers;

public class PostEndTurn(IHubContext<CombatHub> hubContext) : Endpoint<PostEndTurnRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/combat/turn/end");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
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

        await hubContext.NotifyCombatUpdated(result.Value);
        await SendAsync(new()
        {
            Combat = result.Value
        });
    }
}