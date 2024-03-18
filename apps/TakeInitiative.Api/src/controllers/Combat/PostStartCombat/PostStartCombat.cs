using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;
using CSharpFunctionalExtensions;
using TakeInitiative.Utilities;
using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Controllers;

public class PostStartCombat(IDocumentStore Store, IHubContext<CombatHub> hubContext) : Endpoint<PostStartCombatRequest, CombatResponse>
{
	public override void Configure()
	{
		Post("/api/combat/start");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(PostStartCombatRequest req, CancellationToken ct)
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

				if (combat.State != CombatState.Open) {
					ThrowError($"Combat has already been started.");
				}

				// Check the user is part of the combat.
				if (combat.DungeonMaster != userId)
				{
					ThrowError("Must be the dungeon master in order to start the combat.");
				}

				// Publish the event
				CombatStartedEvent activateEvent = new()
				{
					UserId = userId,
				};
				session.Events.Append(req.CombatId, activateEvent);
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