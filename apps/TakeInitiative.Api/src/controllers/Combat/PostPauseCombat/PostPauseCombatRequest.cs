namespace TakeInitiative.Api.Controllers;

public record PostPauseCombatRequest
{
	public required Guid CombatId { get; set; }
}
