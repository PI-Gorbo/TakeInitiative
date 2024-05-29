using System.ComponentModel.DataAnnotations;

namespace TakeInitiative.Api.Features.Campaigns;

public record PutPlayerCharacterRequest
{
    public required Guid CampaignMemberId { get; set; }
    public required Guid PlayerCharacterId { get; set; }
    public required PlayerCharacterDTO PlayerCharacter { get; set; }
}
