using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public class PutCampaignDetails(IDocumentSession session, IHubContext<CampaignHub> campaignHub) : Endpoint<PutCampaignDetailsRequest, Campaign>
{
    public override void Configure()
    {
        Put("/api/campaign");
    }
    public override async Task HandleAsync(PutCampaignDetailsRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var result = await Result
        .Try(
            async () => await session.LoadAsync<Campaign>(req.CampaignId),
            ApiError.DbInteractionFailed)
            .EnsureNotNull("There is no campaign with the given id.")
            .Ensure(c => userId == c.OwnerId, "Only the owner of the campaign can update the details.")
        .Bind(async (campaign) =>
        {
            // Validate the campaign name. Ensure the user doesn't have any other campaigns w/*  */ith that name.
            var campaignCountWithNewName = await session.Query<Campaign>()
                .Where(x => x.CampaignName == req.CampaignName && x.OwnerId == userId)
                .CountAsync();
            if (campaignCountWithNewName != 0)
            {
                return ApiError.Invalid<PutCampaignDetailsRequest>(x => x.CampaignName, "");
            }

            campaign = campaign with
            {
                CampaignDescription = req.CampaignDescription ?? campaign.CampaignDescription,
                CampaignName = req.CampaignName ?? campaign.CampaignName,
                CampaignSettings = req.CampaignSettings ?? campaign.CampaignSettings,
            };

            session.Store(campaign);
            await session.SaveChangesAsync(ct);
            await campaignHub.NotifyCampaignStateUpdated(campaign.Id);
            return Result.Success<Campaign, ApiError>(campaign);
        });

        await this.ReturnApiResult(result);
    }
}