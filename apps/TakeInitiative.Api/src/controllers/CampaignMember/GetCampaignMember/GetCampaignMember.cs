using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Marten.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Controllers;

public class GetCampaignMember(IDocumentStore Store) : Endpoint<GetCampaignMemberRequest, CampaignMember>
{
	public override void Configure()
	{
		Get("/api/campaign/member");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(GetCampaignMemberRequest req, CancellationToken ct)
	{
		var userIdResult = this.User.GetUserId();
		if (userIdResult.IsFailure)
		{
			ThrowError(userIdResult.Error, (int)HttpStatusCode.Unauthorized);
		}

		var result = await Store.Try(async session =>
		{
			// Check that the user is part of the campaign.
			var campaignMember = await session.LoadAsync<CampaignMember>(req.CampaignMemberId);
			var userIsPartOfCampaign = await session.Query<CampaignMember>().Where(x => x.CampaignId == campaignMember.CampaignId && x.UserId == userIdResult.Value)
				.CountAsync() == 1;

			if (!userIsPartOfCampaign)
			{
				ThrowError("User must be part of the campaign to request information about its members", (int)HttpStatusCode.Unauthorized);
			}

			return campaignMember;
		});

		if (result.IsFailure)
		{
			ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
		}

		await SendAsync(result.Value);
	}
}
