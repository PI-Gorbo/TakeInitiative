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
public class GetUser(IDocumentStore Store) : EndpointWithoutRequest<GetUserResponse>
{
	public override void Configure()
	{
		Get("/api/user");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(CancellationToken ct)
	{
		var userId = this.GetUserIdOrThrowUnauthorized();
		var result = await Store.Try(async (IDocumentSession session) =>
			{
				var user = await session.LoadAsync<ApplicationUser>(userId);
				var campaigns = await session.LoadManyAsync<Campaign>(user!.Campaigns);

				var dmCampaigns = new List<GetUserCampaignDto>();
				var memberCampaigns = new List<GetUserCampaignDto>();
				foreach (Campaign campaign in campaigns)
				{
					var member = campaign.CampaignMemberInfo.SingleOrDefault(x => x.UserId == userId);
					if (member!.IsDungeonMaster)
					{
						dmCampaigns.Add(new GetUserCampaignDto(campaign.CampaignName, campaign.Id));
					}
					else
					{
						memberCampaigns.Add(new GetUserCampaignDto(campaign.CampaignName, campaign.Id));
					}
				}

				return new GetUserResponse()
				{
					UserId = user.Id,
					Username = user.UserName,
					DmCampaigns = dmCampaigns,
					MemberCampaigns = memberCampaigns
				};
			});

		if (result.IsFailure)
		{
			ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
		}

		await SendAsync(result.Value);
	}
}