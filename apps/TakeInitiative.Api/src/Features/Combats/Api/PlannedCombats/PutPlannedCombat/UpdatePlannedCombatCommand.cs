using CSharpFunctionalExtensions;

using FastEndpoints;

using Marten;

using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record UpdatePlannedCombatRequest : ICommand<Result<PlannedCombat, ApiError>>
{
    public required Guid UserId { get; set; }
    public required Guid PlannedCombatId { get; set; }
    public required string CombatName { get; set; }
}

public class UpdatePlannedCombatCommand(IDocumentSession session)
    : CommandHandler<UpdatePlannedCombatRequest, Result<PlannedCombat, ApiError>>
{
    public override async Task<Result<PlannedCombat, ApiError>> ExecuteAsync(UpdatePlannedCombatRequest command,
        CancellationToken ct = new CancellationToken())
    {
        // Fetch the planned combat
        var plannedCombat = await session.Query<PlannedCombat>()
            .Where(x => x.Id == command.PlannedCombatId)
            .SingleOrDefaultAsync(ct);

        if (plannedCombat == null)
        {
            return ApiError.BadRequest("There is no planned combat with the provided id.");
        }

        var campaignMember = await session.Query<CampaignMember>()
            .GetCampaignMemberForUserAndCampaign(command.UserId, plannedCombat.CampaignId);
        if (campaignMember == null)
        {
            return ApiError.NotFound("Cannot add a planned combat to a campaign you are not a member of.");
        }

        if (campaignMember.IsDungeonMaster == false)
        {
            return ApiError.BadRequest("Only the dungeon master can add planned combats.");
        }

        // Ensure the name is unique among other combats for the campaign.
        var nameIsUnique = await session.Query<PlannedCombat>()
            .Where(x => x.CombatName == command.CombatName && x.Id == plannedCombat.Id)
            .CountAsync(ct) == 0;
        if (!nameIsUnique)
        {
            return ApiError.BadRequest("There is already another planned combat with that name");
        }

        // Update the name
        plannedCombat.CombatName = command.CombatName;

        session.Store(plannedCombat);
        await session.SaveChangesAsync(ct);

        return plannedCombat;
    }
}