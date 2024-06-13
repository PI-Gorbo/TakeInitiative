using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities.Extensions;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.SignalR;
using Marten.Events.Daemon.Coordination;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public class PutReadAndSaveCampaigns(IDocumentSession session) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Put("/api/admin/readAndSave/campaigns");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await Result.Try(async () =>
        {
            var campaigns = await session.Query<Campaign>().ToListAsync(ct);
            session.StoreObjects(campaigns);
            await session.SaveChangesAsync(ct);
        });
    }
}