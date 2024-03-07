using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;

namespace TakeInitiative.Api.Models;

public class CombatProjection : SingleStreamProjection<Combat>
{
	public Combat Apply(OpenCombatNpcsAddedEvent @event, Combat Combat)
	{
		var idsToRemove = @event.Npcs.Select(x => x.Id).ToArray();
		return Combat with
		{
			InitiativeList = Combat.InitiativeList!
				.RemoveAll(x => x.Id.IsOneOf(idsToRemove))
				.AddRange(@event.Npcs)
		};
	}

	public Combat Apply(OpenCombatNpcRemovedEvent @event, Combat Combat)
	{
		return Combat with
		{
			InitiativeList = Combat.InitiativeList!
				.RemoveAll(x => x.Id == @event.NpcId)
		};
	}

	public Combat Apply(OpenCombatNpcAddedEvent @event, Combat Combat)
	{
		return Combat with
		{
			InitiativeList = Combat.InitiativeList!
				.RemoveAll(x => x.Id == @event.Npc.Id)
				.Add(@event.Npc)
		};
	}

	public async Task<Combat> Apply(OpenCombatPlayerCharactersSetEvent @event, Combat Combat, IEvent<OpenCombatPlayerCharactersSetEvent> eventDetails, IQuerySession session)
	{
		var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
		var characterNames = String.Join(", ", @event.Characters.Select(x => x.Name));
		return Combat with
		{
			CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} plans to join the combat with the character(s) {characterNames}"],
			InitiativeList = Combat.InitiativeList!
				.RemoveAll(x => x is CombatPlayerCharacter combatPlayerCharacter && combatPlayerCharacter.PlayerId == @event.UserId)
				.AddRange(@event.Characters)
		};
	}

	public async Task<Combat> Apply(PlayerLeftEvent @event, Combat Combat, IEvent<PlayerLeftEvent> eventDetails, IQuerySession session)
	{
		var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
		return Combat with
		{
			CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} left the combat at {eventDetails.Timestamp:R}"],
			CurrentPlayers = Combat.CurrentPlayers?.RemoveAll(
				x => x.UserId == @event.UserId
			),
		};
	}

	public async Task<Combat> Apply(PlayerJoinedEvent @event, Combat Combat, IEvent<PlayerJoinedEvent> eventDetails, IQuerySession session)
	{
		var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
		return Combat with
		{
			CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} joined the combat at {eventDetails.Timestamp:R}"],
			CurrentPlayers = Combat.CurrentPlayers?.Add(
				new PlayerDto { UserId = @event.UserId }
			),
		};
	}

	public async Task<Combat> Apply(CombatOpenedEvent @event, Combat Combat, IEvent<CombatOpenedEvent> eventDetails, IQuerySession session)
	{
		var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
		return Combat with
		{
			CampaignId = @event.CampaignId,
			CombatName = @event.CombatName,
			State = CombatState.Open,
			DungeonMaster = @event.UserId,
			Timing = [
				new CombatTimingRecord(StartTime: eventDetails.Timestamp, EndTime: null)
			],
			CombatLogs = [$"Combat started at {eventDetails.Timestamp:R} by {user?.UserName}."],
			CurrentPlayers = [],
			PlannedStages = @event.Stages.ToImmutableList(),
			InitiativeList = [],
		};
	}

}
