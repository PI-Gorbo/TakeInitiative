using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record PostPlannedCombatNpcRequest
{
	public required Guid CombatId { get; set; }
	public required Guid StageId { get; set; }
	public required string Name { get; set; }
	public required CharacterHealth? Health { get; set; } = null;
	public required int? ArmorClass { get; set; } = null;
	public required CharacterInitiative Initiative { get; set; }
	public required uint Quantity { get; set; }

}
