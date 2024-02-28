namespace TakeInitiative.Api.Models;

public record PlannedCombatNonPlayerCharacter : NonPlayerCharacter
{
    public required uint Quantity { get; set; } 

	public static PlannedCombatNonPlayerCharacter New(string Name, CharacterInitiative Initiative, int? ArmorClass = null, CharacterHealth? Health = null, uint Quantity = 1) {
		return new PlannedCombatNonPlayerCharacter() {
			Id = Guid.NewGuid(),
			Initiative = Initiative,
			Name = Name,
			ArmorClass = ArmorClass,
			Health = Health,
			Quantity = Quantity
		};
	}
}
