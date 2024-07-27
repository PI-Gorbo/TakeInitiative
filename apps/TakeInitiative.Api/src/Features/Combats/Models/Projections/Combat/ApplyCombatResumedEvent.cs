using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(CombatResumedEvent @event, Combat Combat, IEvent<CombatResumedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            State = CombatState.Started,
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} resumed the combat at {eventDetails.Timestamp:R}"],
        };
    }
}