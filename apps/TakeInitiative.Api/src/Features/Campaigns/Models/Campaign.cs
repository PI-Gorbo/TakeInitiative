using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Campaigns;

public record Campaign
{
    public required Guid Id { get; init; }
    public required Guid OwnerId { get; init; }
    public required string CampaignName { get; init; }
    public string CampaignDescription { get; init; } = "";
    public string CampaignResources { get; init; } = "";
    public List<Guid> PlannedCombatIds { get; init; } = [];
    public List<CampaignMemberInfo> CampaignMemberInfo { get; init; } = [];
    public Guid? ActiveCombatId { get; init; } = null;
    public DateTimeOffset CreatedTimestamp { get; init; } = DateTimeOffset.UtcNow;

    private CampaignSettings _campaignSettings;
    public CampaignSettings CampaignSettings // This setup allows properties to be added dynamically as they are read in. Auto Initialize props do not work.
    {
        get
        {
            if (_campaignSettings == null)
            {
                _campaignSettings = new CampaignSettings();
            }

            return _campaignSettings;
        }
        init
        {
            _campaignSettings = value;
        }
    }

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