namespace TakeInitiative.Api.Features.Campaigns;

public record PutCampaignMemberResourcesRequest
{
    public Guid MemberId { get; set; }
    public required CampaignMemberResource[] Resources { get; set; }
}
