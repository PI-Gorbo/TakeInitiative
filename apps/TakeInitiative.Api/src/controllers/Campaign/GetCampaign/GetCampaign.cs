using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Data.Commands;

public class GetCampaign(IDocumentStore Store) : Endpoint<GetCampaignRequest, Campaign>
{
	public override void Configure()
	{
		Get("/api/campaign/{CampaignId}");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(GetCampaignRequest req, CancellationToken ct)
	{
		var userId = this.GetUserIdOrThrowUnauthorized();
		var result = await Store.Try(
			async (session) =>
			{
				var campaign = await session.LoadAsync<Campaign>(req.CampaignId, ct);
				if (campaign == null)
				{
					ThrowError("There is no campaign with the given id.", (int)HttpStatusCode.NotFound);
				}

				// Check the user is one of the members of the campaign.
				var userIsAMember = campaign.CampaignMemberInfo.Any(x => x.UserId == userId);
				if (!userIsAMember)
				{
					ThrowError("You cannot retrieve a campaign you are not apart of.", (int)HttpStatusCode.BadRequest);
				}

				return campaign;
			});

		if (result.IsFailure)
		{
			ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
		}

		await SendAsync(result.Value);
	}
}