namespace TakeInitiative.Api.Models;
public record  PlayerCharacter : ICharacter
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid PlayerId { get; set; }
    public required string Name { get; set; }
    public CharacterHealth? Health { get; set; } = null;
    public required CharacterInitiative Initiative { get; set; }
    public int? ArmorClass { get; set; } = null;
}