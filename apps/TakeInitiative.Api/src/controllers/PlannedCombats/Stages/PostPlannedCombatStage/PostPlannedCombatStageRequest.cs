namespace TakeInitiative.Api.Controllers;

public record PostPlannedCombatStageRequest
{
    public required Guid CombatId { get; set; }
    public required string Name { get; set; }
}
