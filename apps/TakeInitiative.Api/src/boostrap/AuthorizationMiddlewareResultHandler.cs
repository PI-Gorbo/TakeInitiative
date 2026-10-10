using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace TakeInitiative.Api.Bootstrap;

//https://khalidabuhakmeh.com/customize-the-authorization-pipeline-in-aspnet-core
public class AuthorizationMiddlewareResultHandler(ILogger<AuthorizationMiddlewareResultHandler> logger) : IAuthorizationMiddlewareResultHandler
{
    private readonly Microsoft.AspNetCore.Authorization.Policy.AuthorizationMiddlewareResultHandler _defaultHandler = new();
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var authorizationFailureReason = authorizeResult.AuthorizationFailure?.FailureReasons.FirstOrDefault();
        var message = authorizationFailureReason?.Message;
        logger.LogInformation("Authorization Result says {Message}",
            message
        );

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}