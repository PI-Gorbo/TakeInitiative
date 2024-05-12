using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Features;

public record GetCampaignResponse
{
    public required Campaign Campaign { get; set; }
    public required CampaignMember UserCampaignMember { get; set; }
    public required CampaignMemberDto[] NonUserCampaignMembers { get; set; }
    public required PlannedCombat[]? PlannedCombats { get; set; }
    public required FinishedCombatDto[] FinishedCombats { get; set; }
    public required string JoinCode { get; set; }
    public required CombatDto? CombatDto { get; set; }
}

public record FinishedCombatDto
{
    public required Guid CombatId { get; set; }
    public required string Name { get; set; }
}