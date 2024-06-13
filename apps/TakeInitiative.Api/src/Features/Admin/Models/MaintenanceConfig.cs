namespace TakeInitiative.Api.Features.Admin;
public record MaintenanceConfig : IAdminConfig
{
    public Guid Id { get; set; }
    public bool InMaintenanceMode { get; set; } = false;
    public string? Reason { get; set; } = null;
}