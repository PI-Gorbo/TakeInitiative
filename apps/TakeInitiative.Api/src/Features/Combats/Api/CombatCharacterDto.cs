namespace TakeInitiative.Api.Features.Combats;

public record CombatCharacterDto
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required CharacterHealth? Health { get; set; }
    public required bool Hidden { get; set; }
    public required int[] InitiativeValue { get; set; }
    public required int? ArmorClass { get; set; }
}
