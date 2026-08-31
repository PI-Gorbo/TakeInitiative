using System.Net;
using System.Security.Claims;
using CSharpFunctionalExtensions;
using FastEndpoints;
using FastEndpoints.Security;

namespace TakeInitiative.Utilities.Extensions;
public static class ClaimsExtensions
{
    /// <summary>
    /// Reads the authenticated user's id out of the claims principal.
    /// Returns a failure when the principal is missing the claim or it is not a Guid,
    /// so callers that cannot throw an HTTP error (SignalR hubs) can decide what to do.
    /// </summary>
    public static Result<Guid> GetUserId(this ClaimsPrincipal? principal)
        => principal
            .AsMaybe()
            .ToResult("There is no authenticated user on this connection.")
            .Bind(user => user
                .ClaimValue("UserID")
                .AsMaybe()
                .ToResult("The claims principal does not have a UserID"))
            .Bind(id => Result.SuccessIf(Guid.TryParse(id, out Guid result), result, "Could not parse user id as Guid"));

    public static Guid GetUserIdOrThrowUnauthorized<TReq, TResp>(this Endpoint<TReq, TResp> endpoint) where TReq : notnull
    {
        var outcome = endpoint.User.GetUserId();

        if (outcome.IsFailure)
        {
            endpoint.ThrowError("Invalid cookie.", (int)HttpStatusCode.Unauthorized);
        }

        return outcome.Value;
    }
}
