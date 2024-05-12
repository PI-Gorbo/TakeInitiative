namespace TakeInitiative.Api.Features;

public record DeletePlannedCombatNpcRequest
{
    public required Guid CombatId { get; set; }
    public required Guid StageId { get; set; }
    public required Guid NpcId { get; set; }
}
