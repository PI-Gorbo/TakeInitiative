using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Api.CQRS;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Controllers;

public class PutStagePlannedCharactersRequest {
    public Guid CombatId {get; set;}
    
    public Dictionary<Guid, StagePlannedCharacterDto[]> PlannedCharactersToStage {get; set;} = null!;
}

public class PutStagePlannedCharacterRequestValidator : Validator<PutStagePlannedCharactersRequest>
{
    public PutStagePlannedCharacterRequestValidator()
    {
        RuleFor(x => x.CombatId).NotEmpty();
        RuleFor(x => x.PlannedCharactersToStage)
            .NotEmpty();
    }
}

public class PutStagePlannedCharacters(IDocumentStore Store, IHubContext<CombatHub> hubContext) : Endpoint<PutStagePlannedCharactersRequest, CombatResponse>
{
	public override void Configure()
	{
		Put("/api/combat/stage/planned");
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