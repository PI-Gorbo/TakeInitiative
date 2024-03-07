namespace TakeInitiative.Api.Models;
public record Character
{
	public required Guid Id { get; set; }
	public required string Name { get; set; }
	public required CharacterHealth? Health { get; set; }
	public required CharacterInitiative Initiative { get; set; }
	public required int? ArmorClass { get; set; }

	public static Character New(string Name, CharacterInitiative initiative, int? ArmorClass = null, CharacterHealth? Health = null)
	{
		return new Character()
		{
			Id = Guid.NewGuid(),
			Initiative = initiative,
			Name = Name,
			ArmorClass = ArmorClass,
			Health = Health
		};
	}
}
