namespace TakeInitiative.Api.Features.Combats;

public record PutPlannedCombatStageRequest
{
    public Guid CombatId { get; set; }
    public Guid StageId { get; set; }
    public string Name { get; set; }
}
