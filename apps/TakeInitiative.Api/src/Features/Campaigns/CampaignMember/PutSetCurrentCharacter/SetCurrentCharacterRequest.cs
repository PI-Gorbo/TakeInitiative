namespace TakeInitiative.Api.Features;

public record SetCurrentCharacterRequest
{
    public Guid MemberId { get; set; }
    public Guid CharacterId { get; set; }
}
