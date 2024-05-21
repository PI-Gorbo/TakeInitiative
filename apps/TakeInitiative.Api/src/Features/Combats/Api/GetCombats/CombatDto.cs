namespace TakeInitiative.Api.Features.Combats;

public record CombatDto
{
    public required Guid CombatId { get; set; }
    public required string CombatName { get; set; }
    public required CombatState State { get; set; }
    public required DateTimeOffset? FinishedTimestamp { get; set; }
}
