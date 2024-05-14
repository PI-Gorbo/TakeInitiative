namespace TakeInitiative.Api.Features.Campaigns;

public record SetCurrentCharacterRequest
{
    public Guid MemberId { get; set; }
    public Guid CharacterId { get; set; }
}
