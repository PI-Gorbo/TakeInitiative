using FastEndpoints;
using Marten;

namespace TakeInitiative.Api.Features.Admin;
public class PutMaintenanceConfig(IDocumentSession session) : Endpoint<MaintenanceConfig>
{
    public override void Configure()
    {
        Put("/api/admin/maintenance");
    }


    public override async Task HandleAsync(MaintenanceConfig cfg, CancellationToken ct)
    {
        // Get or create the maintenance config.
        var maintenanceConfig = await session.Query<MaintenanceConfig>().FirstOrDefaultAsync();

        maintenanceConfig ??= new MaintenanceConfig();
        maintenanceConfig.InMaintenanceMode = cfg.InMaintenanceMode;
        maintenanceConfig.Reason = cfg.Reason;

        session.Store(maintenanceConfig);
        await session.SaveChangesAsync();
        await SendAsync(maintenanceConfig);
    }

}