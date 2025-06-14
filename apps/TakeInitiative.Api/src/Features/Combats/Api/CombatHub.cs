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

    public async Task JoinCombat(IDocumentStore Store, Guid UserId, Guid CombatId)
    {
        // Check the user can join the combat. 
        Result joinedCombatResult = await Store.Try(async (session) =>
        {
            var combat = await session.LoadAsync<Combat>(CombatId);
            if (combat == null)
            {
                return Result.Failure("Combat does not exist.");
            }

            // Check if the user is already part of the combat.
            if (combat.CurrentPlayers.Any(x => x.UserId == UserId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, combat.Id.ToString());
                return Result.Success(combat);
            }

            // Check the user is in the combat, if they are, they can be added to the combat.
            var isApartOfCampaign = await session.UserIsApartOfCampaign(UserId, combat.CampaignId);
            if (!isApartOfCampaign)
            {
                return Result.Failure("Cannot join a combat of a campaign you are not apart of.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, combat!.Id.ToString());

            return Result.Success(combat);
        });

        if (joinedCombatResult.IsFailure)
        {
            throw new OperationCanceledException(joinedCombatResult.Error);
        }

        return;
    }

    public async Task LeaveCombat(IDocumentStore Store, Guid UserId, Guid CombatId)
    {
        // Check the user can leave the combat. 
        Result leaveCombatResult = await Store.Try(async (session) =>
        {
            var combat = await session.LoadAsync<Combat>(CombatId);
            if (combat == null)
            {
                return Result.Failure("Combat does not exist.");
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, combat!.Id.ToString());

            return Result.Success(combat);
        });

        if (leaveCombatResult.IsFailure)
        {
            throw new OperationCanceledException(leaveCombatResult.Error);
        }

        return;
    }
}