using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Campaigns;

public record Campaign
{
    public required Guid Id { get; set; }
    public required Guid OwnerId { get; set; }
    public required string CampaignName { get; set; }
    public string CampaignDescription { get; set; } = "";
    public string CampaignResources { get; set; } = "";
    public List<Guid> PlannedCombatIds { get; set; } = [];
    public List<CampaignMemberInfo> CampaignMemberInfo { get; set; } = [];
    public Guid? ActiveCombatId { get; set; } = null;
    public List<Guid> CombatIds { get; set; } = [];
    public DateTimeOffset CreatedTimestamp { get; init; } = DateTimeOffset.UtcNow;

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
        this.CampaignMemberInfo.Add(campaignMemberInfo);
        return this;
    }

    public bool isDm(Guid userId)
    {
        var memberInfo = CampaignMemberInfo.SingleOrDefault(x => x.UserId == userId);
        if (memberInfo == null)
        {
            return false;
        }

        return memberInfo.IsDungeonMaster;
    }

    public string GetJoinCode() => CampaignIdShortener.ToShortId(this.Id);
}


