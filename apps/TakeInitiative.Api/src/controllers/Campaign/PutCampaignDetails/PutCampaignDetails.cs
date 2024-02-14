

using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using FastEndpoints.Security;
using Marten;
using Marten.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Data.Commands;

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

                campaign.CampaignDescription = req.CampaignDescription;
                campaign.CampaignResources = req.CampaignResources;

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



