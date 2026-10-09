using TakeInitiative.Api.Identity;

namespace TakeInitiative.Api.Features.Users;
public class ApplicationUser : MartenIdentityUser<ApplicationUserRole>
{
    public DateTimeOffset? EmailConfirmationLastSent { get; set; } = null;
    public DateTimeOffset? ResetPasswordLastSent { get; set; } = null;
}