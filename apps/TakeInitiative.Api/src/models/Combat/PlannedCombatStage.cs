namespace TakeInitiative.Api.Models;

public record PlannedCombatStage
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required List<PlannedCombatNonPlayerCharacter> Npcs { get; set; }

    public static PlannedCombatStage New(string name)
    {
        return new PlannedCombatStage()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Npcs = new()
        };
    }
}
