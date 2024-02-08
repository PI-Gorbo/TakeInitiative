using System.Security.Claims;
using CSharpFunctionalExtensions;
using FastEndpoints.Security;

namespace TakeInitiative.Utilities.Extensions;
public static class ClaimsExtensions
{
    public static Result<Guid> GetUserId(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal
            .ClaimValue("UserID")
            .AsMaybe()
            .ToResult("The claims principal does not have a UserID")
            .Bind(id => Result.SuccessIf(Guid.TryParse(id, out Guid result), result, "Could not parse user id as Guid"));
    }
}