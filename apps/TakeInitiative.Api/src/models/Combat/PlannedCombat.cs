namespace TakeInitiative.Api.Models;

public record PlannedCombat
{
  public Guid Id { get; init; } = Guid.NewGuid();
  public Guid CampaignId { get; set; }
  public required string CombatName { get; set; }
  public List<PlannedCombatStage> Stages { get; set; } = [  // Default is a list with a single element.
      new PlannedCombatStage() { Name = "Stage One" }
    ];

  public static PlannedCombat New(Guid CampaignId, string CombatName)
  {
    return new PlannedCombat()
    {
      CombatName = CombatName,
      CampaignId = CampaignId,
    };
  }
}
