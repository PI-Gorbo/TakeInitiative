using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Users;
public class GetUser(IDocumentSession session) : EndpointWithoutRequest<GetUserResponse>
{
    public override void Configure()
    {
        Get("/api/user");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        Result<GetUserResponse, ApiError> result = await GetUserResponse.Generate(session, userId);

        await this.ReturnApiResult(result);
    }

}