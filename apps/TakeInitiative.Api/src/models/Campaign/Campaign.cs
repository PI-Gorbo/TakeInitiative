namespace TakeInitiative.Api.Models;

public record Campaign
{
    public required Guid Id { get; set; }
    public required Guid OwnerId { get; set; }
    public required string CampaignName { get; set; }
    public string CampaignDescription { get; set; } = "";
    public string CampaignResources { get; set; } = "";
    public List<Guid> PlannedCombatIds { get; set; } = [];
    public List<Guid> CampaignMemberIds { get; set; } = [];
    public Guid? ActiveCombatId { get; set; } = null;
    public List<Guid> CombatIds { get; set; } = [];
    public DateTimeOffset CreatedTimestamp { get; init; } = DateTimeOffset.UtcNow;
}
