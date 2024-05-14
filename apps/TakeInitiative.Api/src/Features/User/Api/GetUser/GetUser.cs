using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Users;
public class GetUser(IDocumentSession session) : EndpointWithoutRequest<GetUserResponse>
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
        var result = await Result.Try(async () =>
        {
            var user = await session.LoadAsync<ApplicationUser>(userId);
            var campaigns = await session.LoadManyAsync<Campaign>(user!.Campaigns);
            return new
            {
                user = user,
                campaigns = campaigns
            };
        }, err => ApiError.DbInteractionFailed(err.Message))
        .Ensure((data) => data.user is not null, ApiError.NotFound("There is no user with the given user id."))
        .Ensure(data => data.campaigns is not null, ApiError.NotFound("The user does not belong to any campaigns."))
        .Map(userData =>
        {
            var dmCampaigns = new List<GetUserCampaignDto>();
            var memberCampaigns = new List<GetUserCampaignDto>();
            foreach (Campaign campaign in userData.campaigns)
            {
                var member = campaign.CampaignMemberInfo.SingleOrDefault(x => x.UserId == userId);
                if (member!.IsDungeonMaster)
                {
                    dmCampaigns.Add(new GetUserCampaignDto(campaign.CampaignName, campaign.Id, campaign.GetJoinCode()));
                }
                else
                {
                    memberCampaigns.Add(new GetUserCampaignDto(campaign.CampaignName, campaign.Id, campaign.GetJoinCode()));
                }
            }

            return new GetUserResponse()
            {
                UserId = userData.user.Id,
                Username = userData.user.UserName!,
                DmCampaigns = dmCampaigns,
                MemberCampaigns = memberCampaigns
            };
        });

        await this.ReturnApiResult(result);
    }
}