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
        var newInitiativeList = @event.InitiativeRolls.Select((charInitiative, index) =>
        {
            var stagedCharacter = Combat.StagedList.FirstOrDefault(x => x!.Id == charInitiative.id, null);
            if (stagedCharacter == null)
            {
                return null;
            }

            return InitiativeCharacter.FromStagedCharacter(
                stagedCharacter,
                charInitiative.rolls
            );
        })
        .Where(x => x != null)
        .Cast<InitiativeCharacter>()
        .OrderByDescending(x => x.InitiativeValue, new InitiativeComparer())
        .ToImmutableList();

        List<HistoryEvent> events = [
            new CombatInitiativeRolled {
                Rolls = [..@event.InitiativeRolls.OrderByDescending(x => x.rolls, new InitiativeComparer())
                    .Select(rollDto => new InitiativeRolledDto() {
                        CharacterId = rollDto.id,
                        Roll = rollDto.rolls,
                        CharacterName = newInitiativeList.Find(c => c.Id == rollDto.id)?.Name ?? "UNKNOWN",
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