using System.Net;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;


public class PostPlannedCombatStage(IDocumentStore Store) : Endpoint<PostPlannedCombatStageRequest, PlannedCombat>
{
    public override void Configure()
    {
        Post("/api/campaign/planned-combat/stage");
    }

    public override async Task HandleAsync(PostPlannedCombatStageRequest req, CancellationToken ct)
    {

        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await Store.Try(async session =>
        {
            var combat = await session.LoadAsync<PlannedCombat>(req.CombatId);
            if (combat == null)
            {
                ThrowError(x => x.CombatId, "Combat id does not correlate to any known combats.", (int)HttpStatusCode.NotFound);
            }

            // Verify that the user is a member of the campaign & a DM.
            var campaign = await session.LoadAsync<Campaign>(combat.CampaignId);
            if (campaign == null)
            {
                ThrowError(x => x.CombatId, "The combat does not belong to a campaign.", (int)HttpStatusCode.NotFound);
            }

            if (!campaign.isDm(userId))
            {
                ThrowError("Planned combats can only be edited by DMs.");
            }

            // Attempt to add the stage to the planned combat.
            var result = combat.AddStage(PlannedCombatStage.New(req.Name));
            if (result.IsFailure)
            {
                ThrowError(x => x.Name, result.Error);
            }

            session.Store(combat);
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
