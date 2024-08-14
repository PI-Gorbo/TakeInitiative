using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using FastEndpoints.Security;

namespace TakeInitiative.Utilities.Extensions;
public static class ClaimsExtensions
{
    public static Guid GetUserIdOrThrowUnauthorized<TReq, TResp>(this Endpoint<TReq, TResp> endpoint) where TReq : notnull
    {
        var outcome = endpoint.User
            .ClaimValue("UserID")
            .AsMaybe()
            .ToResult("The claims principal does not have a UserID")
            .Bind(id => Result.SuccessIf(Guid.TryParse(id, out Guid result), result, "Could not parse user id as Guid"));

        if (outcome.IsFailure)
        {
            endpoint.ThrowError("Invalid cookie.", (int)HttpStatusCode.Unauthorized);
        }

        return outcome.Value;
    }
}