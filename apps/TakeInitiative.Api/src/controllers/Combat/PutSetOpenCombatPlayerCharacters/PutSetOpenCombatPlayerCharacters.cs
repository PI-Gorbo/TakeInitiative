using System.Diagnostics.CodeAnalysis;
using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using FluentValidation;
using JasperFx.Core;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Controllers;

public class PutSetOpenCombatPlayerCharacters(IDocumentStore Store, IHubContext<CombatHub> CombatHub) : Endpoint<PutSetOpenCombatPlayerCharactersRequest>
{
	public override void Configure()
	{
		Put("/api/combat/open/characters");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(PutSetOpenCombatPlayerCharactersRequest req, CancellationToken ct)
	{
		var userId = this.GetUserIdOrThrowUnauthorized();

		var result = await Store.Try(
			async (session) =>
			{
				// Check the user is part of the combat.
				var result = await session.Query<Combat>()
					.Where(x => x.Id == req.CombatId)
					.Select(x => new
					{
						x.CurrentPlayers,
						x.State
					}).SingleOrDefaultAsync();

				if (result == null)
				{
					ThrowError(x => x.CombatId, "There is no combat with the given id");
				}

				if (result.State > CombatState.Open) {
					ThrowError("Can only set queued player characters when the combat is open.", (int)HttpStatusCode.BadRequest);
				}

				if (!result.CurrentPlayers.Any(x => x.UserId == userId))
				{
					ThrowError("Player is not part of the combat.", (int)HttpStatusCode.BadRequest);
				}

				// publish the event
				var setCharactersEvent = new OpenCombatPlayerCharactersSetEvent()
				{
					UserId = userId,
					Characters = req.Characters
				};

				session.Events.Append(req.CombatId, setCharactersEvent);

				// Save changes, triggering the projection
				await session.SaveChangesAsync(ct);

				var combat = await session.LoadAsync<Combat>(req.CombatId, ct);

				// Notify the hub
				await CombatHub.NotifyCombatUpdated(combat);

			});

		if (result.IsFailure)
		{
			ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
		}

		await SendOkAsync();
	}
}