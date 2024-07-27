using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(PlayerJoinedEvent @event, Combat Combat, IEvent<PlayerJoinedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} joined the combat at {eventDetails.Timestamp:R}"],
            CurrentPlayers = Combat.CurrentPlayers.Add(
                new PlayerDto { UserId = @event.UserId }
            ),
        };
    }
}