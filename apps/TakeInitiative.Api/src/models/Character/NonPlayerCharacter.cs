namespace TakeInitiative.Api.Models;
public record NonPlayerCharacter : ICharacter
{
	public required Guid Id {get; set;}
    public required string Name { get; set; }
    public required CharacterHealth? Health { get; set; } = null;
    public required int? ArmorClass { get; set; } = null;
    public required CharacterInitiative Initiative { get; set; }

	public static NonPlayerCharacter New(string Name, CharacterInitiative initiative, int? ArmorClass = null, CharacterHealth? Health = null)
	{
		return new NonPlayerCharacter()
		{
			Id = Guid.NewGuid(),
			Initiative = initiative,
			Name = Name,
			ArmorClass = ArmorClass,
			Health = Health
		};
	}
}

