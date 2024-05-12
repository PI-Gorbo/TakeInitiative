namespace TakeInitiative.Api.Features;
public record DeleteCampaignRequest
{
    public required Guid CampaignId { get; set; }
}
