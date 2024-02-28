namespace TakeInitiative.Api.Models;
public record PlayerCharacter : ICharacter
{
	public required Guid Id { get; init; } 
	public required Guid PlayerId { get; set; }
	public required string Name { get; set; }
	public CharacterHealth? Health { get; set; } = null;
	public required CharacterInitiative Initiative { get; set; }
	public int? ArmorClass { get; set; } = null;

	public static PlayerCharacter New(Guid PlayerId, string Name, CharacterInitiative initiative, int? ArmorClass = null, CharacterHealth? Health = null)
	{
		return new PlayerCharacter()
		{
			Id = Guid.NewGuid(),
			Initiative = initiative,
			Name = Name,
			PlayerId = PlayerId,
			ArmorClass = ArmorClass,
			Health = Health
		};
	}
}

