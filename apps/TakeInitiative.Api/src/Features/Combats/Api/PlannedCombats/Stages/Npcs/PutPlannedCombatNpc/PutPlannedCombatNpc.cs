using System.Net;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class PutPlannedCombatNpc(IDocumentStore Store, IDiceRoller roller) : Endpoint<PutPlannedCombatNpcRequest, PlannedCombat>
{
    public override void Configure()
    {
        Put("/api/campaign/planned-combat/stage/npc");
    }

    public override async Task HandleAsync(PutPlannedCombatNpcRequest req, CancellationToken ct)
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

            var stage = combat.Stages.FirstOrDefault(x => x.Id == req.StageId);
            if (stage == null)
            {
                ThrowError(x => x.StageId, "There is no stage with the given id.");
            }

            var npc = stage.Npcs.FirstOrDefault(x => x.Id == req.NpcId);
            if (npc == null)
            {
                ThrowError(x => x.NpcId, "There is no npc with the given id.");
            }

            npc = npc with
            {
                Name = req.Name,
                ArmourClass = req.ArmourClass,
                Health = req.Health,
                Initiative = req.Initiative,
                Quantity = req.Quantity
            };

            var validator = new PlannedCombatCharacterValidator(roller);
            var validationResult = await validator.ValidateAsync(npc, ct);
            if (!validationResult.IsValid)
            {
                ThrowError(validationResult.ToString(", "));
            }

            stage.Npcs = stage.Npcs.Where(x => x.Id != req.NpcId).Append(npc).ToList();

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
