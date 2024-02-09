namespace TakeInitiative.Api.Controllers;

public class GetUserResponse
{
	public required List<GetUserCampaignDto> DmCampaigns { get; set; }
	public required List<GetUserCampaignDto> MemberCampaigns { get; set; }
}
