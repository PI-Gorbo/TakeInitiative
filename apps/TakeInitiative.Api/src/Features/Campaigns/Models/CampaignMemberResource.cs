namespace TakeInitiative.Api.Features.Campaigns;

public enum ResourceVisibilityOptions
{
    Private = 0,
    DMsOnly = 1,
    Public = 2
}

public record CampaignMemberResource
{
    public required string Name { get; set; }
    public required string Link { get; set; }
    public required ResourceVisibilityOptions Visibility { get; set; }
}
