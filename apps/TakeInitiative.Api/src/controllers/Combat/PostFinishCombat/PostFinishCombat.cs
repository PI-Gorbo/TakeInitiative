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

public class PostFinishCombat(IHubContext<CombatHub> hubContext) : Endpoint<PostFinishCombatRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/combat/finish");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(PostFinishCombatRequest req, CancellationToken ct)
    {
        Result<Combat> result = await new FinishCombatCommand()
        {
            CombatId = req.CombatId,
            UserId = this.GetUserIdOrThrowUnauthorized()
        }.ExecuteAsync();

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await hubContext.NotifyCombatUpdated(result.Value);
        await SendAsync(new() { Combat = result.Value });
    }
}