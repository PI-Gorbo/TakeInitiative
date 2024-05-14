namespace TakeInitiative.Api.Features.Campaigns;

public record AddPlayerCharacterRequest
{
    public required Guid CampaignMemberId { get; set; }
    public required PlayerCharacterDTO PlayerCharacter { get; set; }
}
