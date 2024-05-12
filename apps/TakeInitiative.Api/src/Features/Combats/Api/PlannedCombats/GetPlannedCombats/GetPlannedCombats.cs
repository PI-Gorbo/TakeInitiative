using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features;

public class GetPlannedCombats(IDocumentStore Store) : Endpoint<GetPlannedCombatsRequest, GetPlannedCombatsResponse>
{
    public override void Configure()
    {
        Get("/api/campaign/planned-combats");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(GetPlannedCombatsRequest req, CancellationToken ct)
    {

        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await Store.Try(async session =>
        {
            var campaign = await session.LoadAsync<Campaign>(req.CampaignId);
            if (campaign == null)
            {
                ThrowError((req) => req.CampaignId, $"There is no campaign that corresponds to the id {req.CampaignId}.");
            }

            // Check that the user is a dungeon master
            var isDungeonMaster = campaign.CampaignMemberInfo.SingleOrDefault(x => x.UserId == userId)?.IsDungeonMaster ?? false;
            if (!isDungeonMaster)
            {
                ThrowError($"Only dungeon masters can access planned combats", (int)HttpStatusCode.BadRequest);
            }

            var plannedCombats = await session.LoadManyAsync<PlannedCombat>(campaign.PlannedCombatIds);
            if (plannedCombats == null)
            {
                ThrowError($"Failed to retrieve planned combats for the campaign {req.CampaignId}", (int)HttpStatusCode.NotFound);
            }

            return new GetPlannedCombatsResponse()
            {
                PlannedCombats = plannedCombats.ToArray()
            };
        });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(result.Value);
    }
}
