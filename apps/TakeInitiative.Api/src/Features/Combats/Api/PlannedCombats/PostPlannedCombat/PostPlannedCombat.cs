using System.Net;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;
public class PostPlannedCombat(IDocumentStore Store) : Endpoint<PostPlannedCombatRequest, PlannedCombat>
{
    public override void Configure()
    {
        Post("/api/combat/planned");
    }

    public override async Task HandleAsync(PostPlannedCombatRequest req, CancellationToken ct)
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
                ThrowError("Only the dungeon master can add planned combats.", (int)HttpStatusCode.Unauthorized);
            }

            // Create a planned Combat.
            var combat = PlannedCombat.New(req.CampaignId, req.CombatName);

            // Ensure the name is unique among other combats for the campaign.
            var nameIsUnique = await session.Query<PlannedCombat>().Where(x => x.CombatName == combat.CombatName && x.CampaignId == req.CampaignId)
                .CountAsync() == 0;
            if (!nameIsUnique)
            {
                ThrowError((req) => req.CombatName, "There is already another planned combat with that name");
            }

            // Add a reference to the campaign.
            var campaign = await session.LoadAsync<Campaign>(req.CampaignId);
            campaign!.PlannedCombatIds.Add(combat.Id);

            session.Store(combat);
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
