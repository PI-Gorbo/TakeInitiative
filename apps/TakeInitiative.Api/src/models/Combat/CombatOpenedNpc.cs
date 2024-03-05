namespace TakeInitiative.Api.Models;

public record CombatOpenedNpc : PlannedCombatNpc
{
	public required bool InBattlefield { get; set; }

	public static CombatOpenedNpc New(
		Guid StageId,
		string Name,
		CharacterInitiative Initiative,
		int? ArmorClass = null,
		CharacterHealth? Health = null,
		uint Quantity = 1,
		bool InBattlefield = false
	)
	{
		return new CombatOpenedNpc()
		{
			StageId = StageId,
			Id = Guid.NewGuid(),
			Initiative = Initiative,
			Name = Name,
			ArmorClass = ArmorClass,
			Health = Health,
			Quantity = Quantity,
			InBattlefield = InBattlefield
		};
	}
}
