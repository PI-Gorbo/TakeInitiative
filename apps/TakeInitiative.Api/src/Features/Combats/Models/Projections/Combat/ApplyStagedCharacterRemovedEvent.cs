using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(StagedCharacterRemovedEvent @event, Combat Combat, IEvent<StagedCharacterRemovedEvent> eventDetails, IQuerySession session)
    {
        var character = Combat.StagedList.SingleOrDefault(x => x.Id == @event.CharacterId);
        if (character == null)
        {
            return Combat;
        }
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);

        return Combat with
        {
            CombatLogs = [.. Combat.CombatLogs, $"{user?.UserName} unstaged {character.Name} at {eventDetails.Timestamp:R}"],
            StagedList = Combat.StagedList
                .Remove(character)
        };
    }
}
