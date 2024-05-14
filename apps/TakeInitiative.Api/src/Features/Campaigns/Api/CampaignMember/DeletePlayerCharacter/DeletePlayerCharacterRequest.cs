namespace TakeInitiative.Api.Features.Campaigns;

public record DeletePlayerCharacterRequest
{
    public required Guid MemberId { get; set; }
    public required Guid PlayerCharacterId { get; set; }
}
