using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Controllers;
public class PutPlannedCombat(IDocumentStore Store) : Endpoint<PutPlannedCombatRequest, PlannedCombat>
{
	public override void Configure()
	{
		Put("/api/campaign/planned-combat");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(PutPlannedCombatRequest req, CancellationToken ct)
	{
		var userIdResult = this.GetUserIdOrThrowUnauthorized();

		var result = await Store.Try(async session =>
		{
			var campaignMember = await session.Query<CampaignMember>().GetCampaignMemberForUserAndCampaign(userIdResult, req.CampaignId);
			if (campaignMember == null)
			{
				ThrowError("Cannot add a planned combat to a campaign you are not a member of.", (int)HttpStatusCode.NotFound);
			}

			if (campaignMember.IsDungeonMaster == false)
			{
				ThrowError("Only the dungeon master can add planned combats.", (int)HttpStatusCode.Unauthorized);
			}

			var combat = await session.LoadAsync<PlannedCombat>(req.CombatId);
			if (combat == null)
			{
				ThrowError("No combat with the given id exists");
			}

			if (req.CombatName != null && req.CombatName != combat.CombatName)
			{
				// Ensure the new campaign name is unique
				var newCampaignNameIsUnique = await session.Query<PlannedCombat>().CombatNameIsUnique(req.CampaignId, req.CombatName);
				if (!newCampaignNameIsUnique)
				{
					ThrowError($"There is already another combat with the name {req.CombatName}.");
				}

				combat.CombatName = req.CombatName;
			}

			if (req.Stages != null)
			{
				combat.Stages = req.Stages;
			}

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
