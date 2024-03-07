namespace TakeInitiative.Api.Models;
public record PlayerCharacter : Character
{
	public required Guid PlayerId { get; set; }
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

