using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using FastEndpoints.Security;
using Marten;
using Marten.Linq.MatchesSql;
using Marten.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TakeInitiative.Api.Controllers;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Data.Commands;


public class PostCreateCampaign(IDocumentStore Store) : Endpoint<PostCreateCampaignRequest, Campaign>
{
	public override void Configure()
	{
		Post("/api/campaign");
		AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
		Policies(TakePolicies.UserExists);
	}
	public override async Task HandleAsync(PostCreateCampaignRequest req, CancellationToken ct)
	{
		var userId = this.GetUserIdOrThrowUnauthorized();
		var result = await Store.Try(async (session) =>
		{
			var campaign = Campaign.CreateNewCampaign(userId, req.CampaignName);
			var alreadyCampaignWithName = await session.Query<Campaign>()
				.Where(x => x.OwnerId == userId && x.CampaignName == req.CampaignName)
				.AnyAsync();

			if (alreadyCampaignWithName)
			{
				ThrowError("You already own a campaign with that name.", (int)HttpStatusCode.BadRequest);
			}

			// Add the owner as a member in the campaign, set as the dungeon master.
			CampaignMember dungeonMaster = CampaignMember.New(
				CampaignId: campaign.Id,
				UserId: userId,
				IsDungeonMaster: true
			);
			campaign.AddCampaignMemberReference(CampaignMemberInfo.FromMember(dungeonMaster));

			session.Store(campaign);
			session.Store(dungeonMaster);

			// Add a reference to the Application User.
			var user = await session.LoadAsync<ApplicationUser>(userId, ct);
			user?.Campaigns.Add(campaign.Id);
			session.Store(user!);

			await session.SaveChangesAsync(ct);
			return campaign;
		});

		if (result.IsFailure)
		{
			ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
		}
		await SendAsync(result.Value);
	}
}



