using System.Net;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class PutUpdateInitiativeCharacter(IHubContext<CombatHub> hubContext) : Endpoint<PutUpdateInitiativeCharacterRequest, CombatResponse>
{
    public override void Configure()
    {
        Put("/api/combat/initiative/character");
    }

    public override async Task HandleAsync(PutUpdateInitiativeCharacterRequest req, CancellationToken ct)
    {
        var result = await new EditInitiativeCharacterCommand()
        {
            CombatId = req.CombatId,
            UserId = this.GetUserIdOrThrowUnauthorized(),
            CharacterDto = req.Character
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