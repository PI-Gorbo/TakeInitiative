using Microsoft.AspNetCore.Identity;

namespace TakeInitiative.Api.Identity;

public class MartenIdentityUser<TRole> : IdentityUser<Guid>
    where TRole : MartenIdentityRole
{
    public List<MartenIdentityClaim> Claims { get; set; } = [];
    public List<MartenIdentityUserLogin> Logins { get; set; } = [];
    public List<TRole> Roles { get; set; } = [];
    public List<MartenIdentityRecoveryCode> RecoveryCodes { get; set; } = [];

    public List<MartenIdentityUserToken> Tokens { get; set; } = [];
}
