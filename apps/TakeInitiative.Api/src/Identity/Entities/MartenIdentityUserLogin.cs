using Microsoft.AspNetCore.Identity;

namespace TakeInitiative.Api.Identity;

public record MartenIdentityUserLogin
{
    public required string LoginProvider { get; set; }
    public required string ProviderKey { get; set; }
    public required string? ProviderDisplayName { get; set; }

    public static MartenIdentityUserLogin FromLoginInfo(UserLoginInfo login) => new MartenIdentityUserLogin
    {
        ProviderKey = login.ProviderKey,
        LoginProvider = login.LoginProvider,
        ProviderDisplayName = login.ProviderDisplayName,
    };

    public UserLoginInfo ToUserLoginInfo() => new UserLoginInfo(LoginProvider, ProviderKey, ProviderDisplayName);
}
