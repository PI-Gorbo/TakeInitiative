namespace TakeInitiative.Api.Models;

public record CampaignMember
{
    public Guid Id { get; set; }
    public required Guid UserId { get; set; }
    public required Guid CampaignId { get; set; }
    public Guid? CurrentCharacterId { get; set; } = null;
    public List<PlayerCharacter> Characters { get; set; } = [];
    public PlayerCharacter? GetCurrentPlayerCharacter() =>
         CurrentCharacterId == null ? null : this.Characters.FirstOrDefault(x => x.Id == CurrentCharacterId, null);

}
