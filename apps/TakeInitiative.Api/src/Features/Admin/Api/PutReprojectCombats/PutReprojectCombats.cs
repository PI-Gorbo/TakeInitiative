using FastEndpoints;
using TakeInitiative.Utilities.Extensions;
using CSharpFunctionalExtensions;
using Marten.Events.Daemon.Coordination;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public class PutReprojectCombats(IProjectionCoordinator projectionCoordinator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Put("/api/admin/reproject/combat");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        UnitResult<ApiError> result = await Result.Try(async () =>
        {
            var daemon = projectionCoordinator.DaemonForMainDatabase();
            await daemon.RebuildProjectionAsync("combat", ct);
        }, ApiError.DbInteractionFailed)
        .Tap(() => SendOkAsync());

        await this.ReturnApiResult(result);
    }
}