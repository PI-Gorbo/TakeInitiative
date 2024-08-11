using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;



public class GetCombats(IDocumentStore Store) : Endpoint<GetCombatsRequest, GetCombatsResponse>
{
    public override void Configure()
    {
        Get("/api/combats/");
    }
    public async override Task HandleAsync(GetCombatsRequest req, CancellationToken ct)
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

            var combats = await session.Query<Combat>()
                .Where(x => x.CampaignId == req.CampaignId)
                .Select(x => new
                {
                    x.Id,
                    x.CombatName,
                    x.State,
                    x.FinishedTimestamp,
                }).ToListAsync();

            return new GetCombatsResponse()
            {
                PlannedCombats = plannedCombats.ToArray(),
                Combats = combats.Select(x => new CombatDto()
                {
                    CombatId = x.Id,
                    CombatName = x.CombatName!,
                    State = x.State,
                    FinishedTimestamp = x.FinishedTimestamp,
                }).ToArray()
            };
        });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(result.Value);
    }
}
