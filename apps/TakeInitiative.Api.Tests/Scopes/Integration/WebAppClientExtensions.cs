using CSharpFunctionalExtensions;
using Microsoft.Extensions.Primitives;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Users;
namespace TakeInitiative.Api.Tests.Integration;

public static class WebAppClientExtensions
{

    private static Task<Result<TResponse>> Delete<TRequest, TResponse>(this IWebAppClient client, TRequest req, string url, int statusCode = 200) where TRequest : class
    => Result.Try(async () =>
        {
            var result = await client.AlbaHost.Scenario(_ =>
            {
                _.Delete.Json(req).ToUrl(url);
                _.StatusCodeShouldBe(statusCode);
            });
            return await result.ReadAsJsonAsync<TResponse>() ?? throw new InvalidCastException($"Could not cast response to type of {typeof(TResponse).Name}");
        });

    private static Task<Result<TResponse>> Post<TRequest, TResponse>(this IWebAppClient client, TRequest req, string url, int statusCode = 200) where TRequest : class
        => Result.Try(async () =>
            {
                var result = await client.AlbaHost.Scenario(_ =>
                {
                    _.Post.Json(req).ToUrl(url);
                    _.StatusCodeShouldBe(statusCode);
                });
                return await result.ReadAsJsonAsync<TResponse>() ?? throw new InvalidCastException($"Could not cast response to type of {typeof(TResponse).Name}");
            });

    private static Task<Result<TResponse>> Put<TRequest, TResponse>(this IWebAppClient client, TRequest req, string url, int statusCode = 200) where TRequest : class
        => Result.Try(async () =>
            {
                var result = await client.AlbaHost.Scenario(_ =>
                {
                    _.Put.Json(req).ToUrl(url);
                    _.StatusCodeShouldBe(statusCode);
                });
                return await result.ReadAsJsonAsync<TResponse>() ?? throw new InvalidCastException($"Could not cast response to type of {typeof(TResponse).Name}");
            });

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

    public static Task<Result<StringValues>> SignUp(this IWebAppClient client, PostSignUpRequest request)
        => Result.Try(async () =>
            {
                var result = await client.AlbaHost.Scenario(_ =>
                {
                    _.Post.Json(request).ToUrl("/api/signup");
                    _.StatusCodeShouldBe(200);
                });
                var cookie = result.Context.Response.Headers.SetCookie;
                return cookie;
            });

    public static Task<Result<Campaign>> PostCreateCampaign(this IWebAppClient client, PostCreateCampaignRequest request)
        => client.Post<PostCreateCampaignRequest, Campaign>(request, "/api/campaign");
}
