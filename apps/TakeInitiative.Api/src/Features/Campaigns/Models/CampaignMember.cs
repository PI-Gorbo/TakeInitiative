namespace TakeInitiative.Api.Features.Campaigns;

public record CampaignMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid UserId { get; set; }
    public required Guid CampaignId { get; set; }
    public required bool IsDungeonMaster { get; set; }
    public List<PlayerCharacter> Characters { get; set; } = [];
    public CampaignMemberResource[] Resources { get; set; } = [];
    public static CampaignMember New(Guid CampaignId, Guid UserId, bool IsDungeonMaster = false)
    {
        return new CampaignMember()
        {
            CampaignId = CampaignId,
            UserId = UserId,
            IsDungeonMaster = IsDungeonMaster
        };
    }
}
