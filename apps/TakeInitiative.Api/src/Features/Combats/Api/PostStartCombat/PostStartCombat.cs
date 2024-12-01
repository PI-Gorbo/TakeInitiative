using System.Net;
using FastEndpoints;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class PostStartCombat(IHubContext<CampaignHub> campaignHub) : Endpoint<PostStartCombatRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/combat/start");
    }

    public override async Task HandleAsync(PostStartCombatRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await new OpenCombatCommand()
        {
            PlannedCombatId = req.PlannedCombatId,
            UserId = userId
        }.ExecuteAsync();

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(new CombatResponse() { Combat = result.Value });
        await campaignHub.NotifyCampaignStateUpdated(result.Value.CampaignId);
    }
}