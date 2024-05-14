namespace TakeInitiative.Api.Features.Campaigns;

public record CampaignMemberInfo
{
    public required Guid MemberId { get; set; }
    public required Guid UserId { get; set; }
    public required bool IsDungeonMaster { get; set; }
    public static CampaignMemberInfo FromMember(CampaignMember member) => new CampaignMemberInfo() { UserId = member.UserId, MemberId = member.Id, IsDungeonMaster = true };
}


