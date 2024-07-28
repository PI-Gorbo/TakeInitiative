
using Alba;
using CSharpFunctionalExtensions;
using FluentAssertions;
using TakeInitiative.Api.Features.Users;
using TakeInitiative.Utilities;
using Testcontainers.PostgreSql;
namespace TakeInitiative.Api.Tests.Integration;

public interface IWebAppClient
{
    public IAlbaHost AlbaHost { get; }
    public PostgreSqlContainer PostgreSqlContainer { get; }
    public IDiceRoller DiceRollerSubstitute { get; }
}

public static class WebAppClientExtensions
{
    public static Task<Result<GetUserResponse>> GetUser(this IWebAppClient client)
        => Result.Try(async () =>
            {
                var result = await client.AlbaHost.Scenario(_ =>
                {
                    _.Get.Url("/api/user");
                    _.StatusCodeShouldBe(200);
                });

                return await result.ReadAsJsonAsync<GetUserResponse>() ?? throw new InvalidCastException($"Could not cast response to type of {typeof(GetUserResponse).Name}");
            });

    public static Task<Result> SignUp(this IWebAppClient client, PostSignUpRequest request)
        => Result.Try(async () =>
            {
                var result = await client.AlbaHost.Scenario(_ =>
                {
                    _.Post.Json(request).ToUrl("/api/signup");
                    _.StatusCodeShouldBe(200);
                });
                var cookie = result.Context.Response.Headers.SetCookie;

                // Set the cookie before each following request.
                client.AlbaHost.BeforeEachAsync(async (client) =>
                {
                    client.Request.Headers.Cookie = cookie;
                });
            });


}