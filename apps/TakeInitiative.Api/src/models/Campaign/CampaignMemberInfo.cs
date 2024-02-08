namespace TakeInitiative.Api.Models;

public record CampaignMemberInfo
{
    public required Guid MemberId { get; set; }
    public required Guid UserId { get; set; }

    public static CampaignMemberInfo FromMember(CampaignMember member) => new CampaignMemberInfo() { UserId = member.UserId, MemberId = member.Id };
}


