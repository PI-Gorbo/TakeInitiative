namespace TakeInitiative.Api.Features.Combats;

public record PostStartCombatRequest
{
    public Guid PlannedCombatId { get; set; }
}
