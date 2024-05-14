using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;
public class GetCombat(IDocumentStore Store) : Endpoint<GetCombatRequest, CombatResponse>
{
    public override void Configure()
    {
        Get("/api/combat/{Id}");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(GetCombatRequest request, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await Store.Try(
            async (session) =>
            {
                var combat = await session.LoadAsync<Combat>(request.Id, ct);
                if (combat == null)
                {
                    ThrowError(x => x.Id, "There is no combat with the given id.");
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