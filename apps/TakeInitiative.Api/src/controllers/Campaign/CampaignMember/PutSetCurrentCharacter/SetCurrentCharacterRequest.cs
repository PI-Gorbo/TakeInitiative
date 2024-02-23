namespace TakeInitiative.Api.Controllers;

public record SetCurrentCharacterRequest
{
    public Guid MemberId { get; set; }
    public Guid CharacterId { get; set; }
}
