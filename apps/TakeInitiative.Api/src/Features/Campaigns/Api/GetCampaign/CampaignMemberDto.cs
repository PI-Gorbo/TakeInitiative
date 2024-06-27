namespace TakeInitiative.Api.Features.Campaigns;

public record CampaignMemberDto
{
    public required Guid UserId { get; set; }
    public required string? Username { get; set; }
    public required bool IsDungeonMaster { get; set; }
    public required CampaignMemberResource[] Resources { get; set; }

    public static CampaignMemberDto FromMember(CampaignMember member, string? username) =>
        new CampaignMemberDto()
        {
            UserId = member.UserId,
            IsDungeonMaster = member.IsDungeonMaster,
            Username = username,
            Resources = member.Resources,
        };
}
