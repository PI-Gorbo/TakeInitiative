namespace TakeInitiative.Api.Features.Campaigns;

public record CampaignMemberResource
{
    public required string Name { get; set; }
    public required string Link { get; set; }
    public required DateTimeOffset LastUpdated {get; set;} 
}
