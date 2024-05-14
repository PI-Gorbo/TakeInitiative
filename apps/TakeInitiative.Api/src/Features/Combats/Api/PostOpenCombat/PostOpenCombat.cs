using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Api.CQRS;
using TakeInitiative.Api.Features;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class PostOpenCombat(IDocumentSession session, IHubContext<CampaignHub> campaignHub) : Endpoint<PostOpenCombatRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/combat/open");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(PostOpenCombatRequest req, CancellationToken ct)
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
        await campaignHub.NotifyCombatStateUpdated(result.Value.CampaignId);
    }
}