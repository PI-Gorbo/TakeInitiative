namespace TakeInitiative.Api.Features.Campaigns;

public record Campaign
{
    public required Guid Id { get; init; }
    public required Guid OwnerId { get; init; }
    public required string CampaignName { get; init; }
    public string CampaignDescription { get; init; } = "";
    public List<Guid> PlannedCombatIds { get; init; } = [];
    public List<CampaignMemberInfo> CampaignMemberInfo { get; init; } = [];
    public Guid? ActiveCombatId { get; init; } = null; // Implies only one active combat at once.
    public DateTimeOffset CreatedTimestamp { get; init; } = DateTimeOffset.UtcNow;
    public CampaignSettings CampaignSettings { get; set; } = new(); // Initializes with default values.
    public static Campaign CreateNewCampaign(Guid OwnerId, string CampaignName)
    {
        return new Campaign()
        {
            Id = Guid.NewGuid(),
            OwnerId = OwnerId,
            CampaignName = CampaignName
        };
    }

    public Campaign AddCampaignMemberReference(CampaignMemberInfo campaignMemberInfo)
    {
        CampaignMemberInfo.Add(campaignMemberInfo);
        return this;
    }

    public bool IsMember(Guid userId) => CampaignMemberInfo.Any(x => x.UserId == userId);

    public bool isDm(Guid userId)
    {
        var memberInfo = CampaignMemberInfo.SingleOrDefault(x => x.UserId == userId);
        if (memberInfo == null)
        {
            return false;
        }

        return memberInfo.IsDungeonMaster;
    }
}