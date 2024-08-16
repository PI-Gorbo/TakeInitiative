using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(CombatOpenedEvent @event, Combat Combat, IEvent<CombatOpenedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat.New(
            Id: Combat.Id,
            CampaignId: @event.CampaignId,
            CombatName: @event.CombatName,
            State: CombatState.Started,
            DungeonMaster: @event.UserId,
            History: [new() {
                Events = [new CombatStarted()],
                Executor = user!.Id,
                Timestamp = eventDetails.Timestamp
            }],
            CurrentPlayers: [],
            PlannedStages: @event.Stages.ToImmutableList(),
            InitiativeList: [],
            StagedList: []
        );
    }
}