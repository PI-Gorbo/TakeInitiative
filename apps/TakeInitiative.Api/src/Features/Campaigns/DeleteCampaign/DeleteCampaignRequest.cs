namespace TakeInitiative.Api.Controllers;
public record DeleteCampaignRequest
{
    public required Guid CampaignId { get; set; }
}
