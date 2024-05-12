namespace TakeInitiative.Api.Features;

public record PostEndTurnRequest
{
    public Guid CombatId { get; set; }
}
