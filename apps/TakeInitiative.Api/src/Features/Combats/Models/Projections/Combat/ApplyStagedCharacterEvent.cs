using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(StagedCharacterEvent @event, Combat Combat, IEvent<StagedCharacterEvent> eventDetails, IQuerySession session)
    {
        return Combat with
        {
            CurrentPlayers = this.ComputePlayersList(Combat.CurrentPlayers, @event.UserId),
            StagedList = (Combat.StagedList ?? [])
                .Add(@event.Character)
        };
    }
}