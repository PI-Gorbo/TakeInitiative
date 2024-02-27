using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record PostPlannedCombatRequest
{
    public required Guid CampaignId { get; set; }
    public required string CombatName { get; set; }
}
