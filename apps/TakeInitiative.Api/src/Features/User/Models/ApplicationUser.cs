using GP.MartenIdentity;

namespace TakeInitiative.Api.Features.Users;
public class ApplicationUser : MartenIdentityUser<ApplicationUserRole>
{
    public List<Guid> Campaigns { get; set; } = new();
    public DateTimeOffset? EmailConfirmationLastSent { get; set; } = null;
    public DateTimeOffset? ResetPasswordLastSent { get; set; } = null;
}