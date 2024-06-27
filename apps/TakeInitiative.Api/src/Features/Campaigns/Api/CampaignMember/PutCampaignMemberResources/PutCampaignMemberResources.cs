using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public class PutCampaignMemberResources(IDocumentSession session, IHubContext<CampaignHub> campaignHub) : Endpoint<PutCampaignMemberResourcesRequest, CampaignMember>
{
    public override void Configure()
    {
        Put("/api/campaign/member/resources");
    }

    public override async Task HandleAsync(PutCampaignMemberResourcesRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await Result
            .Try(() => session.LoadAsync<CampaignMember>(req.MemberId), ApiError.DbInteractionFailed)
                .EnsureNotNull("There is no campaign member with the provided id.")
                .Ensure(member => member.UserId == userId, "You can only edit the member details of your own campaign member record.")
            .MapTry(async (member) =>
            { // Save the member details
                member.Resources = req.Resources;
                session.Store(member);
                await session.SaveChangesAsync();
                await campaignHub.NotifyCampaignMemberStateUpdated(member);
                return member;
            }, ApiError.DbInteractionFailed);

        await this.ReturnApiResult(result);
    }
}