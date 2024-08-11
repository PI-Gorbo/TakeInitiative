using FastEndpoints;

namespace TakeInitiative.Api.Features.Combats;

public class GetCombatsRequest
{
    [QueryParam]
    public Guid CampaignId { get; set; }
}
