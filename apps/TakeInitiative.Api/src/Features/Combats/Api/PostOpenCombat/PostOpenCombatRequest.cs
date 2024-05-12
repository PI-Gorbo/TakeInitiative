namespace TakeInitiative.Api.Features;

public record PostOpenCombatRequest
{
    public Guid PlannedCombatId { get; set; }
}
