using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities.Extensions;
using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Features.Combats;

public class PostStartCombat(IDocumentSession session, IHubContext<CombatHub> combatHub, IHubContext<CampaignHub> campaignHub) : Endpoint<PostStartCombatRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/combat/start");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(PostStartCombatRequest req, CancellationToken ct)
    {
        var result = await new StartCombatCommand()
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