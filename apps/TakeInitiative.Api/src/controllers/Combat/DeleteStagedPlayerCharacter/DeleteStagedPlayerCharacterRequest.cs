namespace TakeInitiative.Api.Controllers;

public record DeleteStagedPlayerCharacterRequest
{
	public required Guid UserId { get; set; }
	public required Guid CharacterId { get; set; }
	public required Guid CombatId { get; set; }
}
