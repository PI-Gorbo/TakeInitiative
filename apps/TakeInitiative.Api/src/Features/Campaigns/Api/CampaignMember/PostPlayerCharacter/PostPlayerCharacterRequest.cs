namespace TakeInitiative.Api.Features.Campaigns;

public record PostPlayerCharacterRequest
{
    public required Guid CampaignMemberId { get; set; }
    public required PlayerCharacterDTO PlayerCharacter { get; set; }
}
