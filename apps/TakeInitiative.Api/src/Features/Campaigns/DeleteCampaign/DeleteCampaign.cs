using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features;

public class DeleteCampaign(IDocumentStore Store) : Endpoint<DeleteCampaignRequest>
{
    public override void Configure()
    {
        Delete("/api/campaign");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }
    public override async Task HandleAsync(DeleteCampaignRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var result = await Store.Try(async (session) =>
            {
                var campaign = await session.LoadAsync<Campaign>(req.CampaignId);
                if (campaign == null)
                {
                    return Result.Failure<Campaign>("Campaign Id does not correlate to any known campaign.");
                }

                if (campaign.OwnerId != userId)
                {
                    return Result.Failure<Campaign>("Only the owner of the campaign can delete this campaign.");
                }

                session.Delete(campaign);
                await session.SaveChangesAsync(ct);
                return campaign;
            });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.BadRequest);
        }

        await SendOkAsync();
    }
}