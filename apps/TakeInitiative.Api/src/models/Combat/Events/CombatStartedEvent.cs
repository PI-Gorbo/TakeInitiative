using System.Collections.Immutable;

namespace TakeInitiative.Api.Models;

public record CombatStartedEvent
{
    public required Guid UserId { get; init; }
    public required List<(Guid id, int[] rolls)> InitiativeRolls { get; set; }
};
