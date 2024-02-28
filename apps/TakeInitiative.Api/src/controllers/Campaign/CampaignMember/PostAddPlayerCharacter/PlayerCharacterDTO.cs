using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record PlayerCharacterDTO : ICharacter
{
    public required string Name { get; set; }
    public required CharacterInitiative Initiative { get; set; }
    public CharacterHealth? Health { get; set; } = null;
    public int? ArmorClass { get; set; } = null;
}
