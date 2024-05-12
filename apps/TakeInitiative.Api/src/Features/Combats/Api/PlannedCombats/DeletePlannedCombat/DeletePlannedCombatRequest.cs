namespace TakeInitiative.Api.Controllers;

public record DeletePlannedCombatRequest
{
    public required Guid CampaignId { get; set; }
    public required Guid CombatId { get; set; }
}
