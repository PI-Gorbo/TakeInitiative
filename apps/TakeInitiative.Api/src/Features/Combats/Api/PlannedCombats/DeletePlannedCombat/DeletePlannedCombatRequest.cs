namespace TakeInitiative.Api.Features.Combats;

public record DeletePlannedCombatRequest
{
    public required Guid CampaignId { get; set; }
    public required Guid CombatId { get; set; }
}
