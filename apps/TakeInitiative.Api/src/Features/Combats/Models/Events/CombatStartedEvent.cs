namespace TakeInitiative.Api.Features.Combats;
public record CombatStartedEvent
{
    public required Guid UserId { get; init; }
    public required List<CharacterInitiativeRoll> InitiativeRolls { get; set; }
};

