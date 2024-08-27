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
        if (!Combat.InitiativeIndex.HasValue || removeCharacterIndex == -1)
        {
            return Combat;
        }

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
                    new() {
                        Events = [
                            new TurnEnded() {
                                CharacterId = @event.CharacterId,
                            },
                            new CharacterRemoved() {
                                CharacterId = @event.CharacterId,
                            },
                            new TurnStarted() {
                                CharacterId = Combat.InitiativeList[newInitiativeIndex].Id,
                            }
                        ],
                        Executor = @event.UserId,
                        Timestamp = eventDetails.Timestamp
                    }
                ]
            };
        }

        var currentTurnCharacterId = Combat.InitiativeList[Combat.InitiativeIndex.Value].Id;
        var updatedInitiativeList = Combat.InitiativeList.RemoveAt(removeCharacterIndex);
        return Combat with
        {
            InitiativeList = updatedInitiativeList,
            InitiativeIndex = updatedInitiativeList.FindIndex(x => x.Id == currentTurnCharacterId), // In case it has updated
            RoundNumber = Combat.RoundNumber,
            History = [
                .. Combat.History,
                new() {
                    Events = [new CharacterRemoved() {
                        CharacterId = @event.CharacterId
                    }],
                    Executor = @event.UserId,
                    Timestamp = eventDetails.Timestamp
                }
              ]
        };
    }
}