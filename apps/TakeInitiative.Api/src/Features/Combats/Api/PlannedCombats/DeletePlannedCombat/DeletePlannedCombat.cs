using System.Net;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;
public class DeletePlannedCombat(IDocumentStore Store) : Endpoint<DeletePlannedCombatRequest>
{
    public override void Configure()
    {
        Delete("/api/combat/planned");
    }

    public override async Task HandleAsync(DeletePlannedCombatRequest req, CancellationToken ct)
    {

        var userId = this.GetUserIdOrThrowUnauthorized();
        var result = await Store.Try(async session =>
        {
            var campaignMember = await session.Query<CampaignMember>().GetCampaignMemberForUserAndCampaign(userId, req.CampaignId);
            if (campaignMember == null)
            {
                ThrowError("Cannot add a planned combat to a campaign you are not a member of.", (int)HttpStatusCode.NotFound);
            }

            if (campaignMember.IsDungeonMaster == false)
            {
                ThrowError("Only the dungeon master can delete planned combats.", (int)HttpStatusCode.Unauthorized);
            }

            var combat = await session.LoadAsync<PlannedCombat>(req.CombatId);
            if (combat == null)
            {
                ThrowError("No combat with the given id exists.", (int)HttpStatusCode.NotFound);
            }

            // Delete the combat
            session.Delete(combat);

            // Add a reference to the campaign.
            var campaign = await session.LoadAsync<Campaign>(req.CampaignId);
            campaign!.PlannedCombatIds.Remove(combat.Id);

            session.Store(campaign);
            await session.SaveChangesAsync();
            return combat;
        });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(result.Value);
    }
}
