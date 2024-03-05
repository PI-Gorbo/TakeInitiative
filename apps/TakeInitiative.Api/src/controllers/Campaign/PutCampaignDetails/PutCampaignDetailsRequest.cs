namespace TakeInitiative.Api.Controllers;

public record PutCampaignDetailsRequest
{
    public required Guid CampaignId { get; set; }
	public string? CampaignName {get; set;}
    public string? CampaignDescription { get; set; }
    public string? CampaignResources { get; set; }
}



