using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Controllers;

public class DeleteStagedPlayerCharacter(IDocumentStore Store, IHubContext<CombatHub> hubContext) : Endpoint<DeleteStagedPlayerCharacterRequest, CombatResponse>
{
	public override void Configure()
	{
		Delete("/api/combat/stage/player-characters");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(DeleteStagedPlayerCharacterRequest req, CancellationToken ct)
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

			var character = combat.StagedList.SingleOrDefault(x => x.Id == req.CharacterId);
			if (character == null)
			{
				ThrowError(x => x.CharacterId, "There is no character with the given id.");
			}

			// Unless the user is the dungeon master, then the 

			// Publish the event
			StagedCharacterRemovedEvent removedEvent = new()
			{
				UserId = req.UserId,
				CharacterId = req.CharacterId
			};
			session.Events.Append(req.CombatId, removedEvent);
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