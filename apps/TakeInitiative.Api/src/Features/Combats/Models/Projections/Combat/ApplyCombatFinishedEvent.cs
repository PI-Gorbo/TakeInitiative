using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(CombatFinishedEvent @event, Combat Combat, IEvent<CombatFinishedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            State = CombatState.Finished,
            FinishedTimestamp = DateTimeOffset.UtcNow,
            History = [.. Combat.History, new() {
                Events = [new CombatFinished()],
                Executor = user!.Id,
                Timestamp = eventDetails.Timestamp
            }]
        };
    }
}