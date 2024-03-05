namespace TakeInitiative.Api.Controllers;

public record GetCampaignRequest
{
    public required Guid CampaignId { get; set; }
}
