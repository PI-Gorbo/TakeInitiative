using System.Collections.Immutable;

namespace TakeInitiative.Api.Models;

public record CombatStartedEvent
{
	public required Guid UserId { get; init; }
    public required ImmutableDictionary<Guid, int> InitiativeRolls {get; set;}
};
