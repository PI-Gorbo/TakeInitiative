using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public Task<Combat> Apply(CombatResumedEvent @event, Combat Combat, IEvent<CombatResumedEvent> eventDetails, IQuerySession session)
    {
        return Task.FromResult(Combat with
        {
            State = CombatState.InitiativeRolled,
        });
    }
}