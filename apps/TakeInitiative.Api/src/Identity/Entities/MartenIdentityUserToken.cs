using Microsoft.AspNetCore.Identity;

namespace TakeInitiative.Api.Identity;

public record MartenIdentityUserToken
{
    public required string LoginProvider { get; set; }
    public required string Name { get; set; }

    [ProtectedPersonalData]
    public required string Value { get; set; }
}

