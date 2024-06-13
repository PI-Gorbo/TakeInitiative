namespace TakeInitiative.Api.Features.Admin;

public record PutMaintenanceConfigRequest
{
    public bool InMaintenanceMode { get; set; } = false;
    public string? Reason { get; set; } = null;
}
