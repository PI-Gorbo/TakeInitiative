using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Data.Commands;

public class GetCampaign(IDocumentStore Store) : Endpoint<GetCampaignRequest, Campaign>
{
    public override void Configure()
    {
        Get("/api/campaign/{CampaignId}");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(GetCampaignRequest req, CancellationToken ct)
    {
        var result = await Store.Try(
            async (session) => await session.LoadAsync<Campaign>(req.CampaignId, ct)
        ).EnsureNotNull("CampaignId does not correlate to any known campaign.");

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.BadRequest);
        }

        await SendAsync(result.Value);
    }
}