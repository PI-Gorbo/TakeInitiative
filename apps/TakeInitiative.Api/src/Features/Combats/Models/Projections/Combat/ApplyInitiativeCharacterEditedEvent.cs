using System.Collections.Immutable;
using JasperFx.Core;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(InitiativeCharacterEditedEvent @event, Combat Combat, IEvent<InitiativeCharacterEditedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        var (character, index) = Combat.InitiativeList.Select((value, index) => (value, index))!.FirstOrDefault(x => x.value?.Id == @event.Character.Id, (null, -1));
        if (character == null)
        {
            return Combat; // Ignore this character if it cannot be found.
        }

        // Take note of changes.
        List<HistoryEvent> historyEvents = [];

        // Check if the character's health has changed.
        if (character.Health is CharacterHealth.Fixed previousValue
            && @event.Character.Health is CharacterHealth.Fixed newHealthValue
            && previousValue.CurrentHealth != newHealthValue.CurrentHealth)
        {
            historyEvents.Add(new CharacterHealthChanged
            {
                CharacterId = character.Id,
                From = previousValue.CurrentHealth,
                To = newHealthValue.CurrentHealth,
            });
        }

        // Check if the character has gained or lost any conditions.
        var oldConditions = character.Conditions.Select(x => x.Id).ToHashSet();
        var newConditions = @event.Character.Conditions.Select(x => x.Id).ToHashSet();
        var conditionsAdded = @event.Character.Conditions.Except(character.Conditions);
        var conditionsRemoved = character.Conditions.Except(@event.Character.Conditions);
        foreach (var condition in conditionsAdded)
        {
            historyEvents.Add(new CharacterConditionAdded
            {
                CharacterId = character.Id,
                Name = condition.Name,
                ConditionId = condition.Id,
            });
        }

        foreach (var condition in conditionsRemoved)
        {
            historyEvents.Add(new CharacterConditionRemoved
            {
                CharacterId = character.Id,
                ConditionId = condition.Id,
                Name = condition.Name
            });
        }

        // Update history.
        var newHistory = historyEvents.Count != 0
            ? [..Combat.History, new HistoryEntry
            {
                Events = [..historyEvents],
                Executor = @event.UserId,
                Timestamp = eventDetails.Timestamp
            }]
            : Combat.History;

        return Combat with
        {
            InitiativeList = Combat.InitiativeList.SetItem(index, character with
            {
                Name = @event.Character.Name,
                Initiative = @event.Character.Initiative,
                Health = @event.Character.Health,
                Hidden = @event.Character.Hidden,
                ArmourClass = @event.Character.ArmourClass,
                Conditions = @event.Character.Conditions
            }),
            History = newHistory,
        };
    }
}