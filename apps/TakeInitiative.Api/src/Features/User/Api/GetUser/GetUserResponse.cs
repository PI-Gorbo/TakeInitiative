namespace TakeInitiative.Api.Features;

public class GetUserResponse
{
    public required Guid UserId { get; set; }
    public required string Username { get; set; }
    public required List<GetUserCampaignDto> DmCampaigns { get; set; }
    public required List<GetUserCampaignDto> MemberCampaigns { get; set; }
}
