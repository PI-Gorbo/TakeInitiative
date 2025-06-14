using Marten;
using Marten.Events;
using Marten.Events.Aggregation;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public Task<Combat> Apply(TurnEndedEvent @event, Combat Combat, IEvent<TurnEndedEvent> eventDetails, IQuerySession session)
    {
        if (!Combat.InitiativeIndex.HasValue || Combat.InitiativeList.Count == 0 || Combat.InitiativeIndex > Combat.InitiativeList.Count)
        {
            return Task.FromResult(Combat); // WHY??
        }

        var currentCharacterId = Combat.InitiativeList[Combat.InitiativeIndex.Value].Id;
        var (nextInitiativeIndex, nextRoundNumber) = Combat.GetNextTurnInfo();

        // Compose the history information.
        List<HistoryEvent> historyToAppend = [new TurnEnded() { CharacterId = currentCharacterId }];
        if (nextRoundNumber != Combat.RoundNumber)
        {  // The next round has started.
            historyToAppend.Add(new RoundEnded());
        }
        historyToAppend.Add(new TurnStarted()
        {
            CharacterId = Combat.InitiativeList[nextInitiativeIndex].Id,
        });

        return Task.FromResult(Combat with
        {
            InitiativeIndex = nextInitiativeIndex,
            RoundNumber = nextRoundNumber,
            History = [.. Combat.History,
                new() {
                    Events = [..historyToAppend],
                    Executor = @event.UserId,
                    Timestamp = eventDetails.Timestamp
                },
            ]
        });
    }
}