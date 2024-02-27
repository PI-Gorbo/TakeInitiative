namespace TakeInitiative.Api.Models;
public record PlayerCharacter : TCharacter
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid PlayerId { get; set; }
    public required string Name { get; set; }
    public CharacterHealth? Health { get; set; } = null;
    public required CharacterInitiative Initiative { get; set; }
    public int? ArmorClass { get; set; } = null;

    public static PlayerCharacter New(Guid PlayerId, string Name, CharacterInitiative initiative, int? ArmorClass = null, CharacterHealth? Health = null)
    {
        return new PlayerCharacter()
        {
            Initiative = initiative,
            Name = Name,
            PlayerId = PlayerId,
            ArmorClass = ArmorClass,
            Health = Health
        };
    }
}

