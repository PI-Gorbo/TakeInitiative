namespace TakeInitiative.Api.Features.Campaigns;

public record Campaign
{
	public required Guid Id { get; init; }
	public required Guid OwnerId { get; init; }
	public required string CampaignName { get; init; }
	public string CampaignDescription { get; init; } = "";
	public List<CampaignMemberInfo> CampaignMemberInfo { get; init; } = [];
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
		CampaignMemberInfo.Add(campaignMemberInfo);
		return this;
	}

	public bool IsDm(Guid userId) => OwnerId == userId;
}