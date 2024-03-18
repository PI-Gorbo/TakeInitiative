namespace TakeInitiative.Api.Controllers;

public record PostStartCombatRequest
{
	public required Guid CombatId { get; set; }
}
