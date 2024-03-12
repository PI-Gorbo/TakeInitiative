using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Models;

public class CombatProjection : SingleStreamProjection<Combat>
{

	public async Task<Combat> Apply(StagedCharacterEditedEvent @event, Combat Combat, IEvent<StagedCharacterEditedEvent> eventDetails, IQuerySession session)
	{
		var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
		return Combat with
		{
			CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} edited staged character {@event.Character.Name} at {eventDetails.Timestamp:R}"],
			StagedList = Combat.StagedList!
				.ReplaceOrInsert(x => x.Id == @event.Character.Id, @event.Character)
		};
	}

	public async Task<Combat> Apply(StagedCharacterAddedEvent @event, Combat Combat, IEvent<StagedCharacterAddedEvent> eventDetails, IQuerySession session)
	{
		var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
		return Combat with
		{
			CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} staged {@event.Character.Name} at {eventDetails.Timestamp:R}"],
			StagedList = (Combat.StagedList ?? ImmutableList<CombatCharacter>.Empty)
				.Add(@event.Character)
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
		return Combat.New(
			Id: Combat.Id,
			CampaignId: @event.CampaignId,
			CombatName: @event.CombatName,
			State: CombatState.Open,
			DungeonMaster: @event.UserId,
			Timing: [
				new CombatTimingRecord(StartTime: eventDetails.Timestamp, EndTime: null)
			],
			CombatLogs: [$"{user?.UserName} started the Combat at {eventDetails.Timestamp:R}"],
			CurrentPlayers: [],
			PlannedStages: @event.Stages.ToImmutableList(),
			InitiativeList: [],
			StagedList: []
		);
	}

}
