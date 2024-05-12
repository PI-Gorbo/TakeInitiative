namespace TakeInitiative.Api.Features;

public record PostStartCombatRequest
{
    public required Guid CombatId { get; set; }
}
