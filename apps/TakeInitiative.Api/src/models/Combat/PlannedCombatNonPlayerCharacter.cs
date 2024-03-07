namespace TakeInitiative.Api.Models;

public record PlannedCombatNpc : Character
{
	public required Guid StageId { get; set; }
	public required uint Quantity { get; set; }

	public static PlannedCombatNpc New(Guid StageId, string Name, CharacterInitiative Initiative, int? ArmorClass = null, CharacterHealth? Health = null, uint Quantity = 1)
	{
		return new PlannedCombatNpc()
		{
			StageId = StageId,
			Id = Guid.NewGuid(),
			Initiative = Initiative,
			Name = Name,
			ArmorClass = ArmorClass,
			Health = Health,
			Quantity = Quantity
		};
	}
}
