namespace TakeInitiative.Data.Commands;

public record GetCampaignRequest
{
    public required Guid CampaignId { get; set; }
}
