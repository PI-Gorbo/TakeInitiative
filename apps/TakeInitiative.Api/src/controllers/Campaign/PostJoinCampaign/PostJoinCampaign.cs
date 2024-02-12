

using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Data.Commands;

public class PostJoinCampaign(IDocumentStore Store) : Endpoint<JoinCampaignByJoinCodeRequest, Campaign>
{
	public override void Configure()
	{
		Post("/api/campaign/join");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}

	public override async Task HandleAsync(JoinCampaignByJoinCodeRequest req, CancellationToken ct)
	{
		var userIdResult = this.User.GetUserId();
		if (userIdResult.IsFailure)
		{
			ThrowError(userIdResult.Error, (int)HttpStatusCode.Unauthorized);
		}

		var result = await CampaignIdShortener.ToId(req.JoinCode)
			.Bind((Guid campaignId) => Store.Try(async session =>
			{
				// Check if there is a campaign that correlates to this id.
				var campaign = await session.LoadAsync<Campaign>(campaignId, ct);
				if (campaign == null)
				{
					return Result.Failure<Campaign>("Join code is invalid.");
				}

				// Check if the user is already a member of the campaign.
				if (campaign.CampaignMemberInfo.Select(x => x.UserId).Contains(userIdResult.Value))
				{
					return Result.Failure<Campaign>("User is already part of the campaign");
				}

				// Add the user to the campaign's list, and create a CampaignMember entity.
				CampaignMember member = CampaignMember.New(campaignId, userIdResult.Value);
				session.Store(member);

				campaign.CampaignMemberInfo.Add(CampaignMemberInfo.FromMember(member));
				session.Store(campaign);

				// Add a reference to the campaign on the application user
				var user = await session.LoadAsync<ApplicationUser>(userIdResult.Value);
				user?.Campaigns.Add(campaign.Id);
				session.Store(user!);

				await session.SaveChangesAsync(ct);

				return campaign;
			}));


		if (result.IsFailure)
		{
			ThrowError(result.Error, (int)HttpStatusCode.BadRequest);
		}

		await SendAsync(result.Value);
	}
}