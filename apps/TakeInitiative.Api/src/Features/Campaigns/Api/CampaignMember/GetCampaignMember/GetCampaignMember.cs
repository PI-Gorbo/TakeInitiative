using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public class GetCampaignMember(IDocumentStore Store) : Endpoint<GetCampaignMemberRequest, CampaignMember>
{
    public override void Configure()
    {
        Get("/api/campaign/member");
    }

    public override async Task HandleAsync(GetCampaignMemberRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var result = await Store.Try(async session =>
        {
            // Check that the user is part of the campaign.
            var campaignMember = await session.LoadAsync<CampaignMember>(req.CampaignMemberId);
            var userIsPartOfCampaign = await session.Query<CampaignMember>().Where(x => x.CampaignId == campaignMember.CampaignId && x.UserId == userId)
                .CountAsync() == 1;

            if (!userIsPartOfCampaign)
            {
                ThrowError("User must be part of the campaign to request information about its members", (int)HttpStatusCode.BadRequest);
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
