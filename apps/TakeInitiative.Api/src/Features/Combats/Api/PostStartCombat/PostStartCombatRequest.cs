namespace TakeInitiative.Api.Features.Combats;

public record PostStartCombatRequest
{
    public required Guid CombatId { get; set; }
}
