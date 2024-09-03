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
                .AddRange(
                    @event.Characters
                    .Select((x, index) => new StagedCharacter(
                        @event.NewIdsToUse[index],
                        PlayerId: @event.UserId,
                        Name: x.Name,
                        Initiative: x.Initiative,
                        ArmourClass: x.ArmourClass,
                        Health: x.Health,
                        Hidden: Combat.DungeonMaster == user!.Id, // Hidden if its the DM's character.
                        CharacterOriginDetails: CharacterOriginDetails.PlayerCharacter(x.Id),
                        CopyNumber: null
                    ))
                )
        };
    }

}