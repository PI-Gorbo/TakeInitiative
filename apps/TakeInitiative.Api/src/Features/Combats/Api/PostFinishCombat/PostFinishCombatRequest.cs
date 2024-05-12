namespace TakeInitiative.Api.Features;

public record PostFinishCombatRequest
{
    public required Guid CombatId { get; set; }
}
