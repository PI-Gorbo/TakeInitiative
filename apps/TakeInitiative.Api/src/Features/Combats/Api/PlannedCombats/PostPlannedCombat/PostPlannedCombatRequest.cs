namespace TakeInitiative.Api.Features;

public record PostPlannedCombatRequest
{
    public required Guid CampaignId { get; set; }
    public required string CombatName { get; set; }
}
