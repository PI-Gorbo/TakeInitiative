using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(CombatStartedEvent @event, Combat Combat, IEvent<CombatStartedEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        var newInitiativeList = @event.InitiativeRolls.Select((charInitiative, index) =>
        {
            var stagedCharacter = Combat.StagedList.FirstOrDefault(x => x.Id == charInitiative.id, null);
            if (stagedCharacter == null)
            {
                return null;
            }

            return stagedCharacter with
            {
                InitiativeValue = charInitiative.rolls
            };
        })
        .Where(x => x != null)
        .Cast<CombatCharacter>()
        .OrderByDescending(x => x.InitiativeValue, new InitiativeComparer())
        .ToImmutableList();

        return Combat with
        {
            State = CombatState.Started,
            StagedList = [],
            InitiativeList = newInitiativeList,
            RoundNumber = 1,
            InitiativeIndex = 0,
            History = [.. Combat.History, new([new CombatStarted()], user!.Id, eventDetails.Timestamp)]
        };
    }
}