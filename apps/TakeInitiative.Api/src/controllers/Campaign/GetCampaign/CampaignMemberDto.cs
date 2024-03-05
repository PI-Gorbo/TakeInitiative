using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record CampaignMemberDto
{
    public required Guid UserId { get; set; }
    public required string? Username { get; set; }
    public required bool IsDungeonMaster { get; set; }
    public required PlayerCharacter? CurrentCharacter { get; set; }

    public static CampaignMemberDto FromMember(CampaignMember member, string? username) =>
        new CampaignMemberDto()
        {
            UserId = member.UserId,
            CurrentCharacter = member.CurrentCharacterId != null ? member.Characters.SingleOrDefault(x => x.Id == member.CurrentCharacterId) : null,
            IsDungeonMaster = member.IsDungeonMaster,
            Username = username
        };
}
