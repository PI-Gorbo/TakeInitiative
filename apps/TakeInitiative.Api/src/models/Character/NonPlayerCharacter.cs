namespace TakeInitiative.Api.Models;
public record NonPlayerCharacter : TCharacter
{
    public required string Name { get; set; }
    public required CharacterHealth? Health { get; set; } = null;
    public required int? ArmorClass { get; set; } = null;
    public required CharacterInitiative Initiative { get; set; }
}

