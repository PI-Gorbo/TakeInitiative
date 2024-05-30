namespace TakeInitiative.Api.Features.Combats;

public record PutPlannedCombatNpcRequest
{
    public required Guid CombatId { get; set; }
    public required Guid StageId { get; set; }
    public required Guid NpcId { get; set; }
    public required string Name { get; set; }
    public required CharacterHealth? Health { get; set; }
    public required int? ArmourClass { get; set; }
    public required CharacterInitiative Initiative { get; set; }
    public required uint Quantity { get; set; }

}
