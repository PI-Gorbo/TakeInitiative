using Microsoft.AspNetCore.Identity;

namespace TakeInitiative.Api.Features.Users;
public class ApplicationUser : IdentityUser<Guid>
{
    public List<Guid> Campaigns { get; set; } = new();
}