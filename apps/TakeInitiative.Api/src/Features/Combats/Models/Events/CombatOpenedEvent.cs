namespace TakeInitiative.Api.Features.Combats;

public record CombatOpenedEvent // Renamed semantically to CombatStartedEvent.
{
    public required Guid UserId { get; set; }
    public required Guid CampaignId { get; set; }
    public required string CombatName { get; set; }
    public required List<PlannedCombatStage> Stages { get; set; }
};
