namespace TakeInitiative.Api.Features.Campaigns;

public record GetCampaignResponse
{
    public required Campaign Campaign { get; set; }
    public required CampaignMember UserCampaignMember { get; set; }
    public required CampaignMemberDto[] CampaignMembers { get; set; }
    public required string JoinCode { get; set; }
    public required CombatHistoryDto[] CombatHistory { get; set; }
    public required CurrentCombatDto? CurrentCombatInfo { get; set; }
}
