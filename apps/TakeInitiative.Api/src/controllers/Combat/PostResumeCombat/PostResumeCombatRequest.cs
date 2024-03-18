namespace TakeInitiative.Api.Controllers;

public record PostResumeCombatRequest
{
	public required Guid CombatId { get; set; }
}
