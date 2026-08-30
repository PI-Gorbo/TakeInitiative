using System.Security.Claims;

using TakeInitiative.Api.Identity;

namespace TakeInitiative.Api.Features.Users;
public class ApplicationUserRole : MartenIdentityRole
{
    public required IList<Claim> Claims { get; set; }
}

