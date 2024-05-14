namespace TakeInitiative.Api.Features.Combats;

public record PostOpenCombatRequest
{
    public Guid PlannedCombatId { get; set; }
}
