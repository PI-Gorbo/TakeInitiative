namespace TakeInitiative.Api.Features.Campaigns;

public record PutCampaignDetailsRequest
{
    public required Guid CampaignId { get; set; }
    public string? CampaignName { get; set; }
    public string? CampaignDescription { get; set; }
    public string? CampaignResources { get; set; }
}



