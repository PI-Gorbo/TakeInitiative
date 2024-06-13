using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Admin;

public class GetMaintenanceConfig(IDocumentSession session) : EndpointWithoutRequest<MaintenanceConfig>
{
    public override void Configure()
    {
        Get("/api/admin/maintenance");
        Options(opts => opts.RequireCors("MainAppAndAdminApp"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Get or create the maintenance config.
        var maintenanceConfig = await session.Query<MaintenanceConfig>().FirstOrDefaultAsync();
        if (maintenanceConfig == null)
        {
            maintenanceConfig = new MaintenanceConfig();
            session.Store(maintenanceConfig);
            await session.SaveChangesAsync();
        }

        await SendAsync(maintenanceConfig);
    }

}