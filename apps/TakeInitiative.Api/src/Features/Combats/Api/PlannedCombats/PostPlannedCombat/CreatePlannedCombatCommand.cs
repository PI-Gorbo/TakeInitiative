using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record CreatePlannedCombatRequest : ICommand<Result<PlannedCombat, ApiError>>
{
    public required Guid UserId { get; set; }
    public required Guid CampaignId { get; set; }
    public required string CombatName { get; set; }
}

public class CreatePlannedCombatCommand(IDocumentSession session)
    : CommandHandler<CreatePlannedCombatRequest, Result<PlannedCombat, ApiError>>
{
    public override async Task<Result<PlannedCombat, ApiError>> ExecuteAsync(CreatePlannedCombatRequest command,
        CancellationToken ct = new CancellationToken())
    {
        var campaignMember = await session.Query<CampaignMember>()
            .GetCampaignMemberForUserAndCampaign(command.UserId, command.CampaignId);
        if (campaignMember == null)
        {
            return ApiError.NotFound("Cannot add a planned combat to a campaign you are not a member of.");
        }

        if (campaignMember.IsDungeonMaster == false)
        {
            return ApiError.BadRequest("Only the dungeon master can add planned combats.");
        }

        // Create a planned Combat.
        var combat = PlannedCombat.New(command.CampaignId, command.CombatName);

        // Ensure the name is unique among other combats for the campaign.
        var nameIsUnique = await session.Query<PlannedCombat>()
            .Where(x => x.CombatName == combat.CombatName && x.CampaignId == command.CampaignId)
            .CountAsync(ct) == 0;
        if (!nameIsUnique)
        {
            return ApiError.BadRequest("There is already another planned comabt with that name");
        }

        // Add a reference to the campaign.
        var campaign = await session.LoadAsync<Campaign>(command.CampaignId, ct);
        campaign!.PlannedCombatIds.Add(combat.Id);

        session.Store(combat);
        session.Store(campaign);
        await session.SaveChangesAsync(ct);

        return combat;
    }
}