namespace TakeInitiative.Api.Features.Combats;

public record PostEndTurnRequest
{
    public Guid CombatId { get; set; }
}
