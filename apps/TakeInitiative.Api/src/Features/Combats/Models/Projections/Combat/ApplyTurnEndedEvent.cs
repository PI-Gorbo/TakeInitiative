using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(TurnEndedEvent @event, Combat Combat, IEvent<TurnEndedEvent> eventDetails, IQuerySession session)
    {
        if (Combat.InitiativeIndex < 0 || Combat.InitiativeList.Count == 0 || Combat.InitiativeIndex > Combat.InitiativeList.Count)
        {
            return Combat; // WHY??
        }

        var currentCharacterId = Combat.InitiativeList[Combat.InitiativeIndex].Id;
        var (nextInitiativeIndex, nextRoundNumber) = Combat.GetNextTurnInfo();

        // Compose the history information.
        List<HistoryEvent> historyToAppend = [new TurnEnded() { CharacterId = currentCharacterId }];
        if (nextRoundNumber != Combat.RoundNumber)
        {  // The next round has started.
            historyToAppend.Add(new RoundEnded());
        }
        historyToAppend.Add(new TurnStarted()
        {
            CharacterId = Combat.InitiativeList[nextInitiativeIndex].Id
        });

        return Combat with
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
        };
    }
}