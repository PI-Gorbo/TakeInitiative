using System.Security.Claims;

using GP.MartenIdentity;

namespace TakeInitiative.Api.Features.Users;
public class ApplicationUserRole : MartenIdentityRole
{
    public required IList<Claim> Claims { get; set; }
}

