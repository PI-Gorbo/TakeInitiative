namespace TakeInitiative.Api.Models;

public record PlannedCombatStage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public List<PlannedCombatNonPlayerCharacter> NPCs { get; set; } = [];
}
