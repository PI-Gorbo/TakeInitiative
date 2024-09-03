namespace TakeInitiative.Api.Features.Combats;

public record PostRollCombatInitiativeRequest
{
    public required Guid CombatId { get; set; }
}
