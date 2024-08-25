using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(StagedCharactersRolledIntoInitiativeEvent @event, Combat Combat, IEvent<StagedCharactersRolledIntoInitiativeEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);

        // Determine new Initiative list.
        var newInitiativeList = @event.InitiativeRolls.Select((charInitiative, index) =>
        {
            // If there is an existing character, return it with the newly computed rolls.
            var exitingCharacter = Combat.InitiativeList.Find(x => x.Id == charInitiative.id).AsMaybe();
            if (exitingCharacter.HasValue)
            {
                return exitingCharacter.Value with
                {
                    InitiativeValue = charInitiative.rolls
                };
            }

            // Otherwise, we expect there to be a staged character.
            var stagedChar = Combat.StagedList.Find(x => x.Id == charInitiative.id).AsMaybe();
            if (stagedChar.HasNoValue) // If there is not one (due to poor data), then return null, it will be filtered out later.
            {
                return null;
            }

            return stagedChar.Value with
            {
                InitiativeValue = charInitiative.rolls
            };
        })
        .Where(x => x != null)
        .Cast<CombatCharacter>()
        .OrderByDescending(x => x.InitiativeValue, new InitiativeComparer())
        .ToImmutableList();

        // Determine new Staged List
        var stagedCharactersToRemove = @event.InitiativeRolls.Select(x => x.id).ToArray();
        var newStagedList = Combat.StagedList.Where(x => !x.Id.In(stagedCharactersToRemove)).ToImmutableList();

        // Determine new Initiative Index
        Maybe<Guid> characterWithCurrentTurn = Combat.InitiativeList.Count > 0 ? Combat.InitiativeList[Combat.InitiativeIndex].Id : Maybe.None;
        var newInitiativeIndex = characterWithCurrentTurn.HasValue ? newInitiativeList.FindIndex(x => x.Id == characterWithCurrentTurn.Value) : 0; // Maintains the initiative index, so that it still points to the character whos turn it was before.

        // Determine History
        HistoryEntry historyEvent = new HistoryEntry()
        {
            Timestamp = eventDetails.Timestamp,
            Events = [
                new CharactersAddedToInitiative() {
                    NewInitiativeList = @event.InitiativeRolls
                        .Select(x => new InitiativeRolledDto() {
                            CharacterId = x.id,
                            CharacterName = newInitiativeList.Find(x => x.Id == x.Id)?.Name ?? "UNKNOWN",
                            Roll = x.rolls
                        }).ToArray()
                }
            ],
            Executor = @event.UserId,
        };

        if (Combat.InitiativeList.Count == 0) // if there were no characters in the combat, then post a turn started event.
        {
            historyEvent.Events = historyEvent.Events.Add(new TurnStarted
            {
                CharacterId = newInitiativeList.FirstOrDefault()?.Id ?? Guid.Empty,
            });
        }

        return Combat with
        {
            StagedList = newStagedList,
            InitiativeList = newInitiativeList,
            InitiativeIndex = newInitiativeIndex,
            History = [.. Combat.History, historyEvent]
        };
    }
}