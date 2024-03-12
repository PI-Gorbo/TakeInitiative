using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;
using FluentValidation;
using CSharpFunctionalExtensions;
using TakeInitiative.Utilities;
using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Controllers;

public record PutActivateStagedCharacterRequest
{
	public required Guid CharacterId { get; set; }
	public required Guid CombatId { get; set; }
}

public class PutActivateStagedCharacterValidator : Validator<PutActivateStagedCharacterRequest>
{
	public PutActivateStagedCharacterValidator()
	{
		RuleFor(x => x.CharacterId)
			.NotEmpty();

		RuleFor(x => x.CombatId)
			.NotEmpty();
	}
}

public class ActivateStagedCharacter(IDocumentStore Store, IHubContext<CombatHub> hubContext) : Endpoint<PutActivateStagedCharacterRequest, CombatResponse>
{
	public override void Configure()
	{
		Put("/api/combat/activate/staged-character");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(PutActivateStagedCharacterRequest req, CancellationToken ct)
	{
		var userId = this.GetUserIdOrThrowUnauthorized();

		Result<CombatResponse> result = await Store.Try(
			async (session) =>
			{
				var combat = await session.LoadAsync<Combat>(req.CombatId);
				if (combat == null)
				{
					ThrowError(x => x.CombatId, "Combat does not exist.");
				}

				// Check the state of the combat.
				if (combat.State == CombatState.Paused || combat.State == CombatState.Finished)
				{
					ThrowError($"Cannot activate character because the combat is {combat.State.ToString().ToLower()}.");
				}

				if (combat.State == CombatState.Open) {
					ThrowError($"Cannot activate character because the combat is open. Please wait until it has started.");
				}

				// Check the user is part of the combat.
				if (!combat.CurrentPlayers.Any(x => x.UserId == userId))
				{
					ThrowError("Must be a current player in order to stage characters");
				}

				var character = combat.StagedList.SingleOrDefault(x => x.Id == req.CharacterId);
				if (character == null)
				{
					ThrowError(x => x.CharacterId, "There is no character with the given id.");
				}

				// Check the player is authorized to delete the staged character.
				bool isAuthorized = combat.DungeonMaster == userId || character.PlayerId == userId;
				if (!isAuthorized)
				{
					ThrowError("Only the dungeon master or the player that made this character can activate this character.");
				}

				// Roll the initiative.
				var initiativeResult = character.Initiative.RollInitiative();
				if (initiativeResult.IsFailure)
				{
					ThrowError($"There was an error while trying to calculate initiative. {initiativeResult.Error}");
				}

				// Publish the event
				StagedCharacterActivatedEvent removedEvent = new()
				{
					UserId = userId,
					CharacterId = req.CharacterId,
					Initiative = initiativeResult.Value
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