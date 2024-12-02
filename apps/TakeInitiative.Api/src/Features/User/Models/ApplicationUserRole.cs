using System.Security.Claims;

using GP.MartenIdentity;

namespace TakeInitiative.Api.Features.Users;
public class ApplicationUserRole : MartenIdentityRole
{
    public Guid Id { get; set; }

    public required string? Name { get; set; }

    public required string? NormalizedName { get; set; }

    public required IList<Claim> Claims { get; set; }
}

