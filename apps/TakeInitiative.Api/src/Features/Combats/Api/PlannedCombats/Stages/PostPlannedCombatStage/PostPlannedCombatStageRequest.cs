namespace TakeInitiative.Api.Features;

public record PostPlannedCombatStageRequest
{
    public required Guid CombatId { get; set; }
    public required string Name { get; set; }
}
