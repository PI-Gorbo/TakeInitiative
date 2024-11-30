using Marten;
using Microsoft.AspNetCore.Authorization;
using TakeInitiative.Api.Features.Admin;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Bootstrap;

public class RequireNotInMaintenanceModeAuthorizationHandler(IDocumentSession session) : AuthorizationHandler<RequireNotInMaintenanceModeAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RequireNotInMaintenanceModeAuthorizationRequirement requirement)
    {
        // Fetch the current maintenance mode in the database. If it exists.
        var maintenanceConfig = await session.LoadAdminConfig<MaintenanceConfig>();
        if (maintenanceConfig == null || !maintenanceConfig.InMaintenanceMode)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail(new AuthorizationFailureReason(this, "In maintenance mode."));
        return;
    }
}