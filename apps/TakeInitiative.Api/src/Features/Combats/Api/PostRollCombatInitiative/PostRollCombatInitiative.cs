using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities.Extensions;
using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Features.Combats;

public class PostRollCombatInitiative(IDocumentSession session, IHubContext<CombatHub> combatHub, IHubContext<CampaignHub> campaignHub) : Endpoint<PostRollCombatInitiativeRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/combat/roll-initiative");
    }

    public override async Task HandleAsync(PostRollCombatInitiativeRequest req, CancellationToken ct)
    {
        var result = await new RollCombatInitiativeCommand()
        {
            CombatId = req.CombatId,
            UserId = this.GetUserIdOrThrowUnauthorized()
        }.ExecuteAsync();

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(new CombatResponse()
        {
            Combat = result.Value,
        });
        await combatHub.NotifyCombatUpdated(result.Value);
        await campaignHub.NotifyCampaignStateUpdated(result.Value.CampaignId);
    }
}