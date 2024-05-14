using System.Security.Claims;
namespace TakeInitiative.Api.Features.Users;
public class ApplicationUserRole
{
    public Guid Id { get; set; }

    public required string? Name { get; set; }

    public required string? NormalizedName { get; set; }

    public required IList<Claim> Claims { get; set; }
}

