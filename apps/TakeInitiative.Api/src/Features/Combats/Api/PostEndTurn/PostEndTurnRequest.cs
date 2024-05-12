namespace TakeInitiative.Api.Controllers;

public record PostEndTurnRequest
{
    public Guid CombatId { get; set; }
}
