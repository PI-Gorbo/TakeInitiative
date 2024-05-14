using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Api.Features;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;


public class DeletePlannedCombatNpc(IDocumentStore Store) : Endpoint<DeletePlannedCombatNpcRequest, PlannedCombat>
{
    public override void Configure()
    {
        Delete("/api/campaign/planned-combat/stage/npc");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(DeletePlannedCombatNpcRequest req, CancellationToken ct)
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

            var stage = combat.Stages.FirstOrDefault(x => x.Id == req.StageId);
            if (stage == null)
            {
                ThrowError(x => x.StageId, "There is no stage with the given id.");
            }

            if (!stage.Npcs.Any(x => x.Id == req.NpcId))
            {
                ThrowError(x => x.NpcId, "No NPC corresponds to the given id.");
            }

            stage.Npcs = stage.Npcs.Where(x => x.Id != req.NpcId).ToList();

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
