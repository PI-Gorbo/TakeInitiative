using System.Collections.Immutable;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;
public partial class CombatProjection : SingleStreamProjection<Combat>
{
    private ImmutableList<PlayerDto> ComputePlayersList(ImmutableList<PlayerDto> players, Guid playerId)
    {
        if (players.Any(x => x.UserId == playerId))
        {
            return players;
        }

        return players.Add(new PlayerDto() { UserId = playerId });
    }
}