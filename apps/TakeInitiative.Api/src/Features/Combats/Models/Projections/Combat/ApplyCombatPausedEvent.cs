using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public Task<Combat> Apply(CombatPausedEvent @event, Combat Combat, IEvent<CombatPausedEvent> eventDetails, IQuerySession session)
    {
        return Task.FromResult(Combat with
        {
            State = CombatState.Paused,
        });
    }
}