namespace TakeInitiative.Api.Controllers;

public record DeleteStagedCharacterRequest
{
	public required Guid CharacterId { get; set; }
	public required Guid CombatId { get; set; }
}
