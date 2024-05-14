namespace TakeInitiative.Api.Features.Combats;

public record PostFinishCombatRequest
{
    public required Guid CombatId { get; set; }
}
