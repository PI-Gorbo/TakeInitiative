using TakeInitiative.Api.Identity;

namespace TakeInitiative.Api.Features.Users;
public class ApplicationUser : MartenIdentityUser<ApplicationUserRole>
{
    public DateTimeOffset? EmailConfirmationLastSent { get; set; } = null;
    public DateTimeOffset? ResetPasswordLastSent { get; set; } = null;

    /// <summary>The SamStack auth user this is, set on their first SamStack sign-in (SamstackUserLinker).</summary>
    public string? AuthUserId { get; set; } = null;
}