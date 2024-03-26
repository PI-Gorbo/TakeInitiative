using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Controllers;

public class PutUpsertStagedCharacter(IDocumentStore Store, IHubContext<CombatHub> hubContext) : Endpoint<PutUpsertStagedCharacterRequest, CombatResponse>
{
	public override void Configure()
	{
		Put("/api/combat/stage/character");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(PutUpsertStagedCharacterRequest req, CancellationToken ct)
	{
		var userId = this.GetUserIdOrThrowUnauthorized();

		Result<CombatResponse> result = await Store.Try(async (session) =>
		{
			var combat = await session.LoadAsync<Combat>(req.CombatId);
			if (combat == null)
			{
				ThrowError(x => x.CombatId, "Combat does not exist.");
			}

			// Check the state of the combat.
			if (combat.State == CombatState.Paused || combat.State == CombatState.Finished)
			{
				ThrowError($"Cannot stage character because the combat is {combat.State.ToString().ToLower()}.");
			}

			// Check the user is part of the combat.
			if (!combat.CurrentPlayers.Any(x => x.UserId == userId))
			{
				ThrowError("Must be a current player in order to stage enemies");
			}

			var existingCharacter = combat.StagedList.SingleOrDefault(x => x.Id == req.Character.Id);
			var character = new CombatCharacter()
			{
				Id = req.Character.Id,
				PlayerId = existingCharacter?.PlayerId ?? userId,
				Name = req.Character.Name,
				Initiative = req.Character.Initiative,
				Health = req.Character.Health,
				ArmorClass = req.Character.ArmorClass,
				Hidden = req.Character.Hidden,
				InitiativeValue = null,
                PlannedCharacterId = null,
                CopyNumber = null,
			};

			if (existingCharacter != null)
			{
                var userIsAllowedToEditCharacter = existingCharacter?.PlayerId == userId || combat.DungeonMaster == userId;
                if (!userIsAllowedToEditCharacter)
                {
                    ThrowError(x => x.Character, "Only a dungeon master can edit this character.");
                }

				// Create the edit user event
				StagedCharacterEditedEvent editEvent = new()
				{
					UserId = userId,
					Character = character
				};
				session.Events.Append(req.CombatId, editEvent);
			}
			else
			{
				// Create the add user event
				StagedCharacterEvent addEvent = new()
				{
					UserId = userId,
					Character = character
				};
				session.Events.Append(req.CombatId, addEvent);
			}

			await session.SaveChangesAsync();

			combat = await session.LoadAsync<Combat>(req.CombatId);
			await hubContext.NotifyCombatUpdated(combat);

			return new CombatResponse()
			{
				Combat = combat
			};
		});

		if (result.IsFailure)
		{
			ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
		}

		await SendAsync(result.Value);
	}
}