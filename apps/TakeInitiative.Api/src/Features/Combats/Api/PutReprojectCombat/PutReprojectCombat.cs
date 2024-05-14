using System.Net;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Api.Features;
using TakeInitiative.Utilities.Extensions;
using FluentValidation;
using CSharpFunctionalExtensions;
using TakeInitiative.Utilities;
using Microsoft.AspNetCore.SignalR;
using Marten.Events.Daemon.Coordination;

namespace TakeInitiative.Api.Features.Combats;

public class PutReprojectCombat(IDocumentStore Store, IHubContext<CombatHub> hubContext, IProjectionCoordinator projectionCoordinator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Put("/api/combat/reproject");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        Result result = await Store.Try(
            async (session) =>
            {
                var daemon = projectionCoordinator.DaemonForMainDatabase();
                await daemon.RebuildProjectionAsync("combat", ct);
            });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendOkAsync();
    }
}