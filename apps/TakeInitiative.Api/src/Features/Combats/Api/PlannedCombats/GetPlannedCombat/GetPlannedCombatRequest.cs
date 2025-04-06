namespace TakeInitiative.Api.Features.Combats;

public record GetPlannedCombatRequest
{
    public required Guid CampaignId { get; set; }
    public required Guid CombatId { get; set; }
}
