using FluentValidation;

namespace TakeInitiative.Api.Models;

public record PlannedCombat
{
	public required Guid Id { get; init; }
	public required Guid CampaignId { get; set; }
	public required string CombatName { get; set; }
	public required List<PlannedCombatStage> Stages { get; set; }

	public static PlannedCombat New(Guid CampaignId, string CombatName)
	{
		return new PlannedCombat()
		{
			Id = Guid.NewGuid(),
			CombatName = CombatName,
			CampaignId = CampaignId,
			Stages = new() {
				new() {Name = "Stage One"}
			}
		};
	}
}

public class PlannedCombatValidator : AbstractValidator<PlannedCombat>
{
	public PlannedCombatValidator() {
		ClassLevelCascadeMode = CascadeMode.Stop;
		RuleFor(x => x.Id).NotEmpty();
		RuleFor(x => x.CampaignId).NotEmpty();
		RuleFor(x => x.CombatName).NotEmpty();
		RuleForEach(x => x.Stages)
			.SetValidator(new PlannedCombatStageValidator());
	}
}

public class PlannedCombatStageValidator : AbstractValidator<PlannedCombatStage> {
	
}
