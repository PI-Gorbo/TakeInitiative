using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    public async Task<Combat> Apply(StagedPlayerCharacterEvent @event, Combat Combat, IEvent<StagedPlayerCharacterEvent> eventDetails, IQuerySession session)
    {
        var user = await session.LoadAsync<ApplicationUser>(@event.UserId);
        return Combat with
        {
            StagedList = (Combat.StagedList ?? [])
                .AddRange(@event.Characters.Select(x => CombatCharacter.NewCombatCharacter(
                    playerId: @event.UserId,
                    name: x.Name,
                    initiative: x.Initiative,
                    armourClass: x.ArmourClass,
                    health: x.Health,
                    hidden: Combat.DungeonMaster == user!.Id,
                    characterOriginDetails: CharacterOriginDetails.PlayerCharacter(@event.UserId),
                    copyNumber: null))
                )
        };
    }

}