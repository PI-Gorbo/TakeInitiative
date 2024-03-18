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

public class PostPauseCombat(IDocumentStore Store, IHubContext<CombatHub> hubContext) : Endpoint<PostPauseCombatRequest, CombatResponse>
{
	public override void Configure()
	{
		Post("/api/combat/pause");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(PostPauseCombatRequest req, CancellationToken ct)
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

				// Check the user is part of the combat.
				if (combat.DungeonMaster != userId)
				{
					ThrowError("Must be the dungeon master in order to finish the combat.");
				}

				// Publish the event
				CombatPausedEvent @event = new()
				{
					UserId = userId,
				};
				session.Events.Append(req.CombatId, @event);
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