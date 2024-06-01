

using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public class PutCampaignDetails(IDocumentStore Store) : Endpoint<PutCampaignDetailsRequest, Campaign>
{
    public override void Configure()
    {
        Put("/api/campaign");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }
    public override async Task HandleAsync(PutCampaignDetailsRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var result = await Store.Try(async (session) =>
            {
                var campaign = await session.LoadAsync<Campaign>(req.CampaignId);
                if (userId != campaign?.OwnerId)
                {
                    return Result.Failure<Campaign>("Only the owner of the campaign can update the details.");
                }

                campaign = campaign with
                {
                    CampaignDescription = req.CampaignDescription ?? campaign.CampaignDescription,
                    CampaignResources = req.CampaignResources ?? campaign.CampaignResources,
                    CampaignName = req.CampaignName ?? campaign.CampaignName,
                    CampaignSettings = req.CampaignSettings ?? campaign.CampaignSettings,
                };

                session.Store(campaign);
                await session.SaveChangesAsync(ct);
                return campaign;
            });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.BadRequest);
        }
        await SendAsync(result.Value);
    }
}