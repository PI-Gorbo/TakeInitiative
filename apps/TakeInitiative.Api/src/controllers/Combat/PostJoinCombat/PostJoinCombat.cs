using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using JasperFx.Core;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.HttpSys;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Controllers;

public record UnplannedCharacter : ICharacter
{
	public required string Name { get; set; }
	public required CharacterHealth? Health { get; set; }
	public required CharacterInitiative Initiative { get; set; }
	public required int? ArmorClass { get; set; }
}

public record PostJoinCombatRequest
{
	public Guid CombatId { get; set; }
	public required List<PlayerCharacter> Characters { get; set; }
	public required List<UnplannedCharacter> UnplannedCharacters {get; set;}
}

public class PostJoinCombat(IDocumentStore Store) : Endpoint<PostOpenCombatRequest, CombatResponse>
{
	public override void Configure()
	{
		Post("/api/combat/join");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(PostOpenCombatRequest req, CancellationToken ct)
	{
		var userId = this.GetUserIdOrThrowUnauthorized();

		var result = await Store.Try(
			async (session) =>
			{
				// Retrieve the planned combat
				var plannedCombat = await session.LoadAsync<PlannedCombat>(req.PlannedCombatId, ct);
				if (plannedCombat == null)
				{
					ThrowError("There is no planned combat with the given id.", (int)HttpStatusCode.NotFound);
				}

				var campaign = await session.LoadAsync<Campaign>(plannedCombat.CampaignId, ct);
				if (campaign == null)
				{
					ThrowError("Cannot open a combat in a campaign that does not exist", (int)HttpStatusCode.NotFound);
				}

				// Check the user is a dm. 
				if (!campaign.CampaignMemberInfo.Single(x => x.UserId == userId).IsDungeonMaster)
				{
					ThrowError("Only dungeon masters can open combats.", (int)HttpStatusCode.BadRequest);
				}

				// publish the event
				var openEvent = new CombatOpenedEvent()
				{
					UserId = userId,
					CampaignId = plannedCombat.CampaignId,
					CombatName = plannedCombat.CombatName,
					Stages = plannedCombat.Stages,
				};

				var existingActiveCombat = await session.Query<Combat>().AnyAsync(activeCombat => activeCombat.CampaignId == openEvent.CampaignId, ct);
				if (existingActiveCombat)
				{
					ThrowError("There is already an active combat.");
				}

				// Create a new stream
				var stream = session.Events.StartStream<Combat>(openEvent);

				// Delete the planned combat.
				session.Delete(plannedCombat);

				// Set the active combat id
				campaign.ActiveCombatId = stream.Id;
				session.Store(campaign);

				// Save changes, triggering the projection
				await session.SaveChangesAsync(ct);

				var combat = await session.LoadAsync<Combat>(stream.Id, ct);
				return new CombatResponse()
				{
					Combat = combat!
				};
			});

		if (result.IsFailure)
		{
			ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
		}

		await SendAsync(result.Value);
	}
} 