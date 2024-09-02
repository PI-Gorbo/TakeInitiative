using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(CombatInitiativeRolledEvent @event, Combat Combat, IEvent<CombatInitiativeRolledEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        var newInitiativeList = @event.EvaluatedRolls.Select((charInitiative) =>
        {
            var stagedCharacter = Combat.StagedList.FirstOrDefault(x => x!.Id == charInitiative.Key, null);
            if (stagedCharacter == null)
            {
                return null;
            }

            return InitiativeCharacter.FromStagedCharacter(
                stagedCharacter,
                charInitiative.Value.Health,
                charInitiative.Value.Initiative
            );
        })
        .Where(x => x != null)
        .Cast<InitiativeCharacter>()
        .OrderByDescending(x => x.Initiative.Value, new InitiativeComparer())
        .ToImmutableList();

        List<HistoryEvent> events = [
            new CombatInitiativeRolled {
                Rolls = [..@event.EvaluatedRolls.OrderByDescending(x => x.Value.Initiative.Value, new InitiativeComparer())
                    .Select(rollDto => new InitiativeRolledDto() {
                        CharacterId = rollDto.Key,
                        Roll = rollDto.Value.Initiative.Value,
                        CharacterName = newInitiativeList.Find(c => c.Id == rollDto.Key)?.Name ?? "UNKNOWN",
                        RolledHealth = Combat.StagedList.Find(x => x.Id == rollDto.Key)?.Health switch {
                            UnevaluatedCharacterHealth.Roll roll => ((CharacterHealth.Fixed)rollDto.Value.Health).DiceRoll,
                            null => null,
                            _ => null, }
                    })],
            }
        ];

        if (newInitiativeList.Count != 0)
        {
            events.Add(new TurnStarted
            {
                CharacterId = newInitiativeList[0].Id,
            });
        }

        return Combat with
        {
            State = CombatState.InitiativeRolled,
            StagedList = [],
            InitiativeList = newInitiativeList,
            RoundNumber = 1,
            InitiativeIndex = 0,
            History = [.. Combat.History, new() {
                Events = [.. events],
                Executor = user!.Id,
                Timestamp = eventDetails.Timestamp
            }]
        };
    }
}