namespace TakeInitiative.Api.Models;

public record CampaignMember
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public required Guid UserId { get; set; }
	public required Guid CampaignId { get; set; }
	public required bool IsDungeonMaster { get; set; }
	public Guid? CurrentCharacterId { get; set; } = null;
	public List<PlayerCharacter> Characters { get; set; } = [];
	public PlayerCharacter? GetCurrentPlayerCharacter() =>
		 CurrentCharacterId == null ? null : this.Characters.FirstOrDefault(x => x.Id == CurrentCharacterId, null);

	public static CampaignMember New(Guid CampaignId, Guid UserId, bool IsDungeonMaster = false)
	{
		return new CampaignMember()
		{
			CampaignId = CampaignId,
			UserId = UserId,
			IsDungeonMaster = IsDungeonMaster
		};
	}
}
