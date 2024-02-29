using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Controllers;

public class PostPlannedCombatNpc(IDocumentStore Store) : Endpoint<PostPlannedCombatNpcRequest, PlannedCombat>
{
	public override void Configure()
	{
		Post("/api/campaign/planned-combat/stage/npc");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(PostPlannedCombatNpcRequest req, CancellationToken ct)
	{

		var userId = this.GetUserIdOrThrowUnauthorized();

		var result = await Store.Try(async session =>
		{
			var combat = await session.LoadAsync<PlannedCombat>(req.CombatId);
			if (combat == null)
			{
				ThrowError(x => x.CombatId, "Combat id does not correlate to any known combats.", (int)HttpStatusCode.NotFound);
			}

			// Verify that the user is a member of the campaign & a DM.
			var campaign = await session.LoadAsync<Campaign>(combat.CampaignId);
			if (campaign == null)
			{
				ThrowError(x => x.CombatId, "The combat does not belong to a campaign.", (int)HttpStatusCode.NotFound);
			}

			if (!campaign.isDm(userId))
			{
				ThrowError("Planned combats can only be edited by DMs.");
			}

			var stage = combat.Stages.FirstOrDefault(x => x.Id == req.StageId);
			if (stage == null) {
				ThrowError(x => x.StageId, "There is no stage with the given id.");
			}

			var npc = PlannedCombatNonPlayerCharacter.New(
				Name: req.Name, 
				Initiative: req.Initiative, 
				ArmorClass: req.ArmorClass, 
				Health: req.Health
			);
			var validator = new PlannedCombatNonPlayerCharacterValidator();
			var validationResult = await validator.ValidateAsync(npc, ct);
			if (!validationResult.IsValid) {
				ThrowError(validationResult.ToString(", "));
			}
			stage.Npcs.Add(npc);
			
			session.Store(combat);
			await session.SaveChangesAsync();

			return combat;
		});

		if (result.IsFailure)
		{
			ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
		}

		await SendAsync(result.Value);
	}
}
