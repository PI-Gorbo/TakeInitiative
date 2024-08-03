using CSharpFunctionalExtensions;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public static class CombatHubContextExtensions
{
    public static Task NotifyCombatUpdated(this IHubContext<CombatHub> hubContext, Combat combat)
    {
        return hubContext.Clients.Group(combat.Id.ToString())
            .SendAsync("combatUpdated", combat);
    }
}

[Authorize]
public class CombatHub : Hub
{
    public Task NotifyCombatUpdated(Combat combat)
    {
        return Clients.Group(combat.Id.ToString())
            .SendAsync("combatUpdated", combat);
    }

    public async Task JoinCombat(IDocumentStore Store, Guid CombatId)
    {
        // Check the user can join the combat. 
        Result joinedCombatResult = await this.Context.User!.GetUserId()
        .Bind((userId) => Store.Try(async (session) =>
        {
            // Verify the combat exists and the player is a member of the campaign the combat is apart of.
            Guid? campaignId = await session.Query<Combat>().Where(x => x.Id == CombatId).Select(x => x.CampaignId).SingleOrDefaultAsync(null);
            if (!campaignId.HasValue)
            {
                return Result.Failure("Combat with provided id does not exist.");
            }

            // Verify the user is a member of the campaign.
            var campaignMemberExists = await session.Query<CampaignMember>().Where(x => x.UserId == userId && x.CampaignId == campaignId).AnyAsync();
            if (!campaignMemberExists)
            {
                return Result.Failure("In order to join the combat, you must be a member of the combat's campaign.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, CombatId.ToString());
            return Result.Success();
        }));

        if (joinedCombatResult.IsFailure)
        {
            throw new OperationCanceledException(joinedCombatResult.Error);
        }

        return;
    }

    public async Task LeaveCombat(Guid CombatId)
    {
        // Check the user can leave the combat. 
        Result leaveCombatResult = await Context.User!.GetUserId()
        .TapTry(async (userId) => await Groups.RemoveFromGroupAsync(Context.ConnectionId, CombatId.ToString()));

        if (leaveCombatResult.IsFailure)
        {
            throw new OperationCanceledException(leaveCombatResult.Error);
        }

        return;
    }
}