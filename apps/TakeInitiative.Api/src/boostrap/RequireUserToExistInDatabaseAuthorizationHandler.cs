using CSharpFunctionalExtensions;
using Marten;
using Microsoft.AspNetCore.Authorization;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Bootstrap;

public class RequireUserToExistInDatabaseAuthorizationHandler : AuthorizationHandler<RequireUserToExistInDatabaseAuthorizationRequirement>
{
    private readonly IDocumentStore store;

    public RequireUserToExistInDatabaseAuthorizationHandler(IDocumentStore Store)
    {
        store = Store;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RequireUserToExistInDatabaseAuthorizationRequirement requirement)
    {
        // Validate the user's existence in the database.
        Result<bool> userExistsResult = await Result.SuccessIf(
            Guid.TryParse(context.User.Claims.SingleOrDefault(x => x.Type == "UserId")?.Value, out Guid parsedValue),
            parsedValue,
            "Failed to parse UserId in claims as Guid.")
        .Bind((id) => store.Try(async (session) =>
        {
            return await session.Query<ApplicationUser>().AnyAsync(x => x.Id == id);
        })).Ensure(userExists =>
        {
            return userExists;
        }, "User does not exist.");

        if (userExistsResult.IsFailure)
        {
            context.Fail(new AuthorizationFailureReason(this, userExistsResult.Error));
            return;
        }

        context.Succeed(requirement);
    }
}