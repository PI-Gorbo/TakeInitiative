namespace TakeInitiative.Api.Features.Combats;

public record CombatStartedEvent // Renamed semantically to CombatInitiativeRolledEvent.
{
    public required Guid UserId { get; init; }
    public required List<CharacterInitiativeRoll> InitiativeRolls { get; set; }
};

