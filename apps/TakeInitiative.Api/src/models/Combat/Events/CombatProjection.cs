using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;

namespace TakeInitiative.Api.Models;

public class CombatProjection : SingleStreamProjection<Combat>
{
	public async Task<Combat> Apply(PlayerJoinedEvent @event, Combat Combat, IEvent<PlayerJoinedEvent> eventDetails, IQuerySession session)
	{
		var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
		return Combat with
		{
			CombatLogs = [$"{user?.UserName} joined the combat at {eventDetails.Timestamp:R}"],
			CurrentPlayers = Combat.CurrentPlayers?.Add(
				new PlayerDto(@event.UserId, @event.MemberId)
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
