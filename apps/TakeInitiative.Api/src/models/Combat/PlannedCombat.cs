using CSharpFunctionalExtensions;
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
				new() {Id= Guid.NewGuid(),Name = "Stage One", Npcs= []}
			}
		};
	}

	public Result<PlannedCombat> AddStage(PlannedCombatStage stage)
	{
		return Result.FailureIf(this.Stages.Any(x => x.Name == stage.Name), $"The combat already contains a stage with the name {stage.Name}.")
			.Map(() =>
			{
				this.Stages.Add(stage);
				return this;
			});
	}

	public Result<PlannedCombat> RemoveStage(Guid stageId)
	{
		// Check the stage exists
		if (!this.Stages.Any(x => x.Id == stageId))
		{
			return Result.Failure<PlannedCombat>($"There is no stage with the id {stageId}");
		}

		Stages = Stages.Where(x => x.Id != stageId).ToList();

		return this;
	}

	public Result<PlannedCombat> AddNpcToStage(Guid stageId, PlannedCombatNonPlayerCharacter npc)
	{
		var stage = this.Stages.SingleOrDefault(x => x.Id == stageId);
		if (stage == null)
		{
			return Result.Failure<PlannedCombat>("Stage does not exist");
		}

		return Result.FailureIf(stage.Npcs.Any(x => x.Name == npc.Name), "There is already an NPC with that name.")
			.Map(() =>
			{
				stage.Npcs.Add(npc);
				return this;
			});
	}
}

public class PlannedCombatValidator : AbstractValidator<PlannedCombat>
{
	public PlannedCombatValidator()
	{
		ClassLevelCascadeMode = CascadeMode.Stop;
		RuleFor(x => x.Id).NotEmpty();
		RuleFor(x => x.CampaignId).NotEmpty();
		RuleFor(x => x.CombatName).NotEmpty();
		RuleForEach(x => x.Stages)
			.SetValidator(new PlannedCombatStageValidator());
	}
}

public class PlannedCombatStageValidator : AbstractValidator<PlannedCombatStage>
{

}
