using System.Net;
using FastEndpoints;
using Marten;
using TakeInitiative.Api.Features;
using TakeInitiative.Utilities.Extensions;
using CSharpFunctionalExtensions;
using TakeInitiative.Api.CQRS;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Features.Combats;

public class PostFinishCombat(IDocumentSession session, IHubContext<CombatHub> combatHub, IHubContext<CampaignHub> campaignHub) : Endpoint<PostFinishCombatRequest, CombatResponse>
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

        await SendAsync(new() { Combat = result.Value });
        await combatHub.NotifyCombatUpdated(result.Value);
        await campaignHub.NotifyCombatStateUpdated(result.Value.CampaignId);
    }
}