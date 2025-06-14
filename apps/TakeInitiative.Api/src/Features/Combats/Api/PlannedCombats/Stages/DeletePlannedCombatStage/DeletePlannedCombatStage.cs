using System.Net;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;


public class DeletePlannedCombatStage(IDocumentStore Store) : Endpoint<DeletePlannedCombatStageRequest, PlannedCombat>
{
    public override void Configure()
    {
        Delete("/api/campaign/planned-combat/stage");
    }

    public override async Task HandleAsync(DeletePlannedCombatStageRequest req, CancellationToken ct)
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

            if (!campaign.IsDm(userId))
            {
                ThrowError("Planned combats can only be edited by DMs.");
            }

            // Attempt to add the stage to the planned combat.
            var result = combat.RemoveStage(req.StageId);
            if (result.IsFailure)
            {
                ThrowError(x => x.StageId, result.Error, (int)HttpStatusCode.BadRequest);
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
