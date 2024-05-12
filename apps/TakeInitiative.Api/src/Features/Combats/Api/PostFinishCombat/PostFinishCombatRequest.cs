namespace TakeInitiative.Api.Controllers;

public record PostFinishCombatRequest
{
	public required Guid CombatId { get; set; }
}
