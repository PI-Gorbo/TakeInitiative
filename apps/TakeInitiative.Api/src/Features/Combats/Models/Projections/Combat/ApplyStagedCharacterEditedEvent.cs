using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(StagedCharacterEditedEvent @event, Combat Combat, IEvent<StagedCharacterEditedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            StagedList = Combat.StagedList!
                .ReplaceOrInsertIfExists(x => x.Id == @event.Character.Id, @event.Character)
        };
    }
}