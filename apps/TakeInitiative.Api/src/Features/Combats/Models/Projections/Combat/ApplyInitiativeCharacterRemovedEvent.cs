using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(InitiativeCharacterRemovedEvent @event, Combat Combat, IEvent<InitiativeCharacterRemovedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        var (character, index) = Combat.InitiativeList.Select((value, index) => (value, index)).FirstOrDefault(x => x.value.Id == @event.CharacterId, (null, -1));
        if (character == null)
        {
            return Combat;
        }

        var initiativeIndex = Combat.InitiativeIndex;
        var roundNumber = Combat.RoundNumber;
        if (index == Combat.InitiativeIndex)
        {
            (initiativeIndex, roundNumber) = Combat.GetNextTurnInfo();
        }

        return Combat with
        {
            InitiativeList = Combat.InitiativeList.RemoveAt(index),
            InitiativeIndex = initiativeIndex,
            RoundNumber = roundNumber
        };
    }
}