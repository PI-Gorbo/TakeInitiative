namespace TakeInitiative.Api.Features.Combats;

public record DeletePlannedCombatStageRequest
{
    public required Guid CombatId { get; set; }
    public required Guid StageId { get; set; }
}
