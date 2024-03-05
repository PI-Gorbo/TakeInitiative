using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record GetCampaignResponse
{
	public required Campaign Campaign { get; set; }
	public required CampaignMember UserCampaignMember { get; set; }
	public required CampaignMemberDto[] NonUserCampaignMembers { get; set; }
	public required PlannedCombat[]? PlannedCombats { get; set; }
	public required string JoinCode { get; set; }
	public required CombatDto? CombatDto { get; set; }
}
