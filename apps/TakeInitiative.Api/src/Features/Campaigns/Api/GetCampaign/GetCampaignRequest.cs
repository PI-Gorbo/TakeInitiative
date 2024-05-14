namespace TakeInitiative.Api.Features.Campaigns;

public record GetCampaignRequest
{
    public required Guid CampaignId { get; set; }
}
