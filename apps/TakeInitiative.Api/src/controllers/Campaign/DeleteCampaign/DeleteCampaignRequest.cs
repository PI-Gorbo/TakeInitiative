using Marten;

namespace TakeInitiative.Data.Commands;
public record DeleteCampaignRequest
{
    public required Guid CampaignId { get; set; }
}
