using CSharpFunctionalExtensions;

using FastEndpoints;

using Marten;
using Marten.Linq.Members;

using Microsoft.AspNetCore.Identity;

using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Users;

public class PutUsername(IDocumentSession session, UserManager<ApplicationUser> userManager) : Endpoint<PutUsernameRequest>
{
    public override void Configure()
    {
        Put("/api/user/username");
    }

    public override async Task HandleAsync(PutUsernameRequest request, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var updateUserResult = await Result.Try(() => session.LoadAsync<ApplicationUser>(userId), ApiError.DbInteractionFailed)
            .EnsureNotNull("Failed to find user with the given userid")
            .MapTry(user => userManager.SetUserNameAsync(user, request.NewUsername), ApiError.DbInteractionFailed)
            .Bind(res =>
            {
                if (res.Succeeded)
                {
                    return UnitResult.Success<ApiError>();
                }

                return UnitResult.Failure<ApiError>(string.Join(", ", res.Errors.Select(err => err.Description)));
            });

        await this.ReturnApiResult(updateUserResult);
    }

}