namespace TakeInitiative.Api.Controllers;

public record PostOpenCombatRequest
{
	public Guid PlannedCombatId { get; set; }
}
