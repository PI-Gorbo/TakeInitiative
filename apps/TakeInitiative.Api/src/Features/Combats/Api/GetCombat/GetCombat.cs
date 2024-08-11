using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;
public class GetCombat(IDocumentStore Store) : EndpointWithoutRequest<CombatResponse>
{
    public override void Configure()
    {
        Get("/api/combat/{Id}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var combatId = Route<Guid>("Id");
        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await Store.Try(
            async (session) =>
            {
                var combat = await session.LoadAsync<Combat>(combatId, ct);
                if (combat == null)
                {
                    ThrowError("There is no combat with the given id.");
                }

                // Fetch the campaign and check the user is apart of the campaign
                var userIsInCampaign = await session.Query<CampaignMember>()
                    .AnyAsync(x => x.CampaignId == combat.CampaignId && x.UserId == userId);

                if (!userIsInCampaign)
                {
                    ThrowError("You cannot view combats of a campaign you are not apart of.");
                }

                return new CombatResponse()
                {
                    Combat = combat
                };
            });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(result.Value);
    }
}