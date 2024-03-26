using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Api.CQRS;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Controllers;

public class PostStagePlannedCharacters(IDocumentStore Store, IHubContext<CombatHub> hubContext) : Endpoint<PutStagePlannedCharactersRequest, CombatResponse>
{
	public override void Configure()
	{
		Post("/api/combat/stage/planned-character");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(PutStagePlannedCharactersRequest req, CancellationToken ct)
	{
		var userId = this.GetUserIdOrThrowUnauthorized();

		Result<Combat> result = await new StagePlannedCharactersCommand() {
            CombatId = req.CombatId,
            UserId = userId,
            PlannedCharactersToStage = req.PlannedCharactersToStage
        }.ExecuteAsync();

		if (result.IsFailure)
		{
			ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
		}


        await hubContext.NotifyCombatUpdated(result.Value);
		await SendAsync(new() {Combat = result.Value});
	}
}