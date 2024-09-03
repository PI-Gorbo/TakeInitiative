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
        var newInitiativeList = @event.Rolls.Select((characterRoll) =>
        {
            // If there is an existing character, return it with the newly computed rolls.
            var exitingCharacter = Combat.InitiativeList.Find(x => x.Id == characterRoll.Key).AsMaybe();
            if (exitingCharacter.HasValue)
            {
                return exitingCharacter.Value with
                {
                    Initiative = characterRoll.Value.Initiative,
                    Health = characterRoll.Value.Health,
                };
            }

            // Otherwise, we expect there to be a staged character.
            var stagedChar = Combat.StagedList.Find(x => x.Id == characterRoll.Key).AsMaybe();
            if (stagedChar.HasNoValue) // If there is not one (due to poor data), then return null, it will be filtered out later.
            {
                return null;
            }

            return InitiativeCharacter.FromStagedCharacter(stagedChar.Value, characterRoll.Value.Health, characterRoll.Value.Initiative);
        })
        .Where(x => x != null)
        .Cast<InitiativeCharacter>()
        .OrderByDescending(x => x.Initiative.Value, new InitiativeComparer())
        .ToImmutableList();

        // Determine new Staged List
        var stagedCharactersToRemove = @event.Rolls.Keys.ToArray();
        var newStagedList = Combat.StagedList.Where(x => !x.Id.In(stagedCharactersToRemove)).ToImmutableList();

        // Determine new Initiative Index
        Maybe<Guid> characterWithCurrentTurn = Combat.InitiativeIndex.HasValue && Combat.InitiativeList.Count > 0 ? Combat.InitiativeList[Combat.InitiativeIndex.Value].Id : Maybe.None;
        var newInitiativeIndex = characterWithCurrentTurn.HasValue ? newInitiativeList.FindIndex(x => x.Id == characterWithCurrentTurn.Value) : 0; // Maintains the initiative index, so that it still points to the character whos turn it was before.

        // Determine History
        HistoryEntry historyEvent = new HistoryEntry()
        {
            Timestamp = eventDetails.Timestamp,
            Events = [
                new CombatInitiativeModified() {
                    NewInitiativeList = @event.Rolls
                        .Select(x => new InitiativeRolledDto() {
                            CharacterId = x.Key,
                            CharacterName = newInitiativeList.Find(init => init.Id == x.Key)?.Name ?? "UNKNOWN",
                            Roll = x.Value.Initiative.Value,
                            RolledHealth = Combat.StagedList.Find(staged => staged.Id == x.Key)?.Health switch {
                                UnevaluatedCharacterHealth.Roll => ((CharacterHealth.Fixed)x.Value.Health).DiceRoll,
                                null => null,
                                _ => null
                             }
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