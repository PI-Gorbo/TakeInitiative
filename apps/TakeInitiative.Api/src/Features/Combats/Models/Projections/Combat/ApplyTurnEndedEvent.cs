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
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        var dungeonMasterEndedTurn = user?.Id == Combat.DungeonMaster;
        var isDungeonMastersTurn = Combat.InitiativeList[Combat.InitiativeIndex].PlayerId == user?.Id;
        var consoleMessage = dungeonMasterEndedTurn && !isDungeonMastersTurn
            ? $"{user?.UserName} had their turn ended by the DM at {eventDetails.Timestamp:R}"
            : $"{user?.UserName} ended their turn at {eventDetails.Timestamp:R}";

        var (nextInitiativeIndex, nextRoundNumber) = Combat.GetNextTurnInfo();

        return Combat with
        {
            InitiativeIndex = nextInitiativeIndex,
            RoundNumber = nextRoundNumber,
            CombatLogs = Combat.CombatLogs.Add(consoleMessage),
        };
    }
}