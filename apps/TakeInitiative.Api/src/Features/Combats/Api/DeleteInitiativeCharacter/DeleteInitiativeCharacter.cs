using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Api.CQRS;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features;

public class DeleteInitiativeCharacter(IHubContext<CombatHub> hubContext) : Endpoint<DeleteInitiativeCharacterRequest, CombatResponse>
{
    public override void Configure()
    {
        Delete("/api/combat/initiative/character");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(DeleteInitiativeCharacterRequest req, CancellationToken ct)
    {
        var result = await new DeleteInitiativeCharacterCommand()
        {
            CombatId = req.CombatId,
            UserId = this.GetUserIdOrThrowUnauthorized(),
            CharacterId = req.CharacterId,
        }.ExecuteAsync();

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(new CombatResponse()
        {
            Combat = result.Value,
        });

        await hubContext.NotifyCombatUpdated(result.Value);
    }
}