namespace TakeInitiative.Api.Features.Combats;

public record PutPlannedCombatRequest
{
    public Guid PlannedCombatId { get; set; }
    public required string CombatName { get; set; }
}