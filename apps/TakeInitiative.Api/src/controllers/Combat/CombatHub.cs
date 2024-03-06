using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using JasperFx.Core;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Controllers;

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
				return Result.Success(combat);
			}

			// Check the user is in the combat, if they are, they can be added to the combat.
			var isApartOfCampaign = await session.UserIsApartOfCampaign(UserId, combat.CampaignId);
			if (!isApartOfCampaign)
			{
				return Result.Failure("Cannot join a combat of a campaign you are not apart of.");
			}

			// Create a join campaign event.
			PlayerJoinedEvent @event = new PlayerJoinedEvent() { UserId = UserId };
			var stream = session.Events.Append(CombatId, @event);
			await session.SaveChangesAsync();

			combat = await session.LoadAsync<Combat>(CombatId);
			await Groups.AddToGroupAsync(Context.ConnectionId, combat.Id.ToString());
			await NotifyCombatUpdated(combat);

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

			// Check if the user is already part of the combat.
			if (!combat.CurrentPlayers.Any(x => x.UserId == UserId))
			{
				return Result.Failure("User is not apart of the combat.");
			}

			// Create a join campaign event.
			PlayerLeftEvent @event = new PlayerLeftEvent() { UserId = UserId };
			var stream = session.Events.Append(CombatId, @event);
			await session.SaveChangesAsync();

			combat = await session.LoadAsync<Combat>(CombatId);

			await NotifyCombatUpdated(combat);
			await Groups.RemoveFromGroupAsync(Context.ConnectionId, combat.Id.ToString());

			return Result.Success(combat);
		});

		if (leaveCombatResult.IsFailure)
		{
			throw new OperationCanceledException(leaveCombatResult.Error);
		}

		return;
	} 
}