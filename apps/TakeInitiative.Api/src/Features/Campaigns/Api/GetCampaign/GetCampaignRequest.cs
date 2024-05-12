namespace TakeInitiative.Api.Features;

public record GetCampaignRequest
{
    public required Guid CampaignId { get; set; }
}
