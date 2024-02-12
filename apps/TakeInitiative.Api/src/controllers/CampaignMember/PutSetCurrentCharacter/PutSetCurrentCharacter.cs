using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Controllers;

public class PutSetCurrentCharacter(IDocumentStore Store) : Endpoint<SetCurrentCharacterRequest, CampaignMember>
{
	public override void Configure()
	{
		Put("/api/campaign/member/character");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(SetCurrentCharacterRequest req, CancellationToken ct)
	{
		var userIdResult = this.User.GetUserId();
		if (userIdResult.IsFailure)
		{
			ThrowError(userIdResult.Error, (int)HttpStatusCode.Unauthorized);
		}

		var result = await Store.Try(async session =>
		{
			var campaignMember = await session.LoadAsync<CampaignMember>(req.MemberId);
			if (campaignMember == null)
			{
				ThrowError("No Campaign Member with the given id exists.", (int)HttpStatusCode.NotFound);
			}

			if (campaignMember.UserId != userIdResult.Value)
			{
				ThrowError("Cannot edit Campaign Member details of others", (int)HttpStatusCode.Unauthorized);
			}

			// Verify the character exists.
			var characterExists = campaignMember.Characters.Where(x => x.Id == req.CharacterId).Count() == 1;
			if (!characterExists)
			{
				ThrowError("The id given doesn't correspond to any character", (int)HttpStatusCode.NotFound);
			}

			campaignMember.CurrentCharacterId = req.CharacterId;

			session.Store(campaignMember);
			await session.SaveChangesAsync();
			return campaignMember;
		});

		if (result.IsFailure)
		{
			ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
		}

		await SendAsync(result.Value);
	}
}
