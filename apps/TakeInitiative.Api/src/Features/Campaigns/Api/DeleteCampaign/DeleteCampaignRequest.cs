namespace TakeInitiative.Api.Features.Campaigns;
public record DeleteCampaignRequest
{
    public required Guid CampaignId { get; set; }
}
