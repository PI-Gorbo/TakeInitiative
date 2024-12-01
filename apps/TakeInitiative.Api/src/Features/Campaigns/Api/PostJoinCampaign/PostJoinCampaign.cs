using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public class PostJoinCampaign(IDocumentSession session) : Endpoint<JoinCampaignByJoinCodeRequest, Campaign>
{
    public override void Configure()
    {
        Post("/api/campaign/join");
    }

    public override async Task HandleAsync(JoinCampaignByJoinCodeRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await CampaignIdShortener
            .ToId(req.JoinCode).MapError(ApiError.BadRequest)
            .Bind<Guid, Campaign, ApiError>(async (Guid campaignId) =>
            {
                // Check if there is a campaign that correlates to this id.
                var campaign = await session.LoadAsync<Campaign>(campaignId, ct);
                if (campaign == null)
                {
                    return ApiError.Invalid<JoinCampaignByJoinCodeRequest>(x => x.JoinCode, "Join code is invalid");
                }

                // Check if the user is already a member of the campaign.
                if (campaign.CampaignMemberInfo.Select(x => x.UserId).Contains(userId))
                {
                    return campaign; // Return early if the user is already part of the campaign.
                }

                // Add the user to the campaign's list, and create a CampaignMember entity.
                CampaignMember member = CampaignMember.New(campaignId, userId);
                session.Store(member);

                campaign.CampaignMemberInfo.Add(CampaignMemberInfo.FromMember(member));
                session.Store(campaign);

                // Add a reference to the campaign on the application user
                var user = await session.LoadAsync<ApplicationUser>(userId);
                user?.Campaigns.Add(campaign.Id);
                session.Store(user!);

                await session.SaveChangesAsync(ct);

                return campaign;
            });

        await this.ReturnApiResult(result);
    }
}