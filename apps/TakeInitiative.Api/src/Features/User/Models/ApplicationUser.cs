using Microsoft.AspNetCore.Identity;

namespace TakeInitiative.Api.Features.Users;
public class ApplicationUser : IdentityUser<Guid>
{
    public List<Guid> Campaigns { get; set; } = new();
    public DateTimeOffset? EmailConfirmationLastSent { get; set; } = null;
    public DateTimeOffset? ResetPasswordLastSent { get; set; } = null;
}