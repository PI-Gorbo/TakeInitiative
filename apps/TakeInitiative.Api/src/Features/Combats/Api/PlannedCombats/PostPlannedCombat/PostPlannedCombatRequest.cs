namespace TakeInitiative.Api.Features.Combats;

public record PostPlannedCombatRequest
{
    public required Guid CampaignId { get; set; }
    public required string CombatName { get; set; }
}
