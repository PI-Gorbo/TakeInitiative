namespace TakeInitiative.Data.Commands;

public record PutCampaignDetailsRequest
{
    public required Guid CampaignId { get; set; }
    public required string CampaignDescription { get; set; }
    public required string CampaignResources { get; set; }
}



