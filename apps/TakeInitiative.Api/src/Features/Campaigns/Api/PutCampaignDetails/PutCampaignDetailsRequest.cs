namespace TakeInitiative.Api.Features.Campaigns;

public record PutCampaignDetailsRequest
{
    public required Guid CampaignId { get; set; }
    public string? CampaignName { get; set; } = null;
    public string? CampaignDescription { get; set; } = null;
    public string? CampaignResources { get; set; } = null;
    public CampaignSettings? CampaignSettings { get; set; } = null;
}

