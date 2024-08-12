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

        // Determine the new initiative & round numbers.
        var removeCharacterIndex = Combat.InitiativeList.FindIndex(x => x.Id == @event.CharacterId);
        if (removeCharacterIndex == Combat.InitiativeIndex) // It is the character who is being removed turn. 
        {
            var (newInitiativeIndex, newRoundNumber) = Combat.GetNextTurnInfo();
            return Combat with
            {
                InitiativeList = Combat.InitiativeList.RemoveAt(removeCharacterIndex),
                InitiativeIndex = newInitiativeIndex,
                RoundNumber = newRoundNumber,
                History = [
                    ..Combat.History,
                    new(
                        [
                            new TurnEnded(@event.CharacterId),
                            new CharacterRemoved(@event.CharacterId),
                            new TurnStarted(Combat.InitiativeList[newInitiativeIndex].Id)
                        ],
                        @event.UserId,
                        eventDetails.Timestamp
                    )
                ]
            };
        }

        var currentTurnCharacterId = Combat.InitiativeList[Combat.InitiativeIndex].Id;
        var updatedInitiativeList = Combat.InitiativeList.RemoveAt(removeCharacterIndex);
        return Combat with
        {
            InitiativeList = updatedInitiativeList,
            InitiativeIndex = updatedInitiativeList.FindIndex(x => x.Id == currentTurnCharacterId), // In case it has updated
            RoundNumber = Combat.RoundNumber,
            History = [
                .. Combat.History,
                new( [ new CharacterRemoved(@event.CharacterId) ], @event.UserId, eventDetails.Timestamp)
              ]
        };
    }
}