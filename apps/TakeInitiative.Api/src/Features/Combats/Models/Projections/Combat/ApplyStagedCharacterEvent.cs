using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public Task<Combat> Apply(StagedCharacterEvent @event, Combat Combat, IEvent<StagedCharacterEvent> eventDetails, IQuerySession session)
    {
        return Task.FromResult(
            Combat with
            {
                CurrentPlayers = ComputePlayersList(Combat.CurrentPlayers, @event.UserId),
                StagedList = (Combat.StagedList ?? [])
                .Add(@event.Character)
            }
        );
    }
}