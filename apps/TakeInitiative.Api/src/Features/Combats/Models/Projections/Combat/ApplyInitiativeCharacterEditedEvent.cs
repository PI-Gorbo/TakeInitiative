using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(InitiativeCharacterEditedEvent @event, Combat Combat, IEvent<InitiativeCharacterEditedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        var (character, index) = Combat.InitiativeList.Select((value, index) => (value, index)).FirstOrDefault(x => x.value.Id == @event.Character.Id, (null, -1));
        if (character == null)
        {
            return Combat; // Ignore this character if it cannot be found.
        }

        return Combat with
        {
            InitiativeList = Combat.InitiativeList.SetItem(index, character with
            {
                Name = @event.Character.Name,
                InitiativeValue = @event.Character.InitiativeValue,
                Health = @event.Character.Health,
                Hidden = @event.Character.Hidden,
                ArmourClass = @event.Character.ArmourClass,
            }),
        };
    }
}