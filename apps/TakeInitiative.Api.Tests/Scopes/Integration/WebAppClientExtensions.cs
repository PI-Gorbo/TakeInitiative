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

    public static Task<Result<CampaignResponse>> PostCreateCampaign(this IWebAppClient client, PostCreateCampaignRequest request)
        => client.Post<PostCreateCampaignRequest, CampaignResponse>(request, "/api/campaigns");

    public static Task<Result<CampaignResponse>> PostJoinCampaign(this IWebAppClient client, PostJoinCampaignRequest request)
        => client.Post<PostJoinCampaignRequest, CampaignResponse>(request, "/api/campaigns/join");

    public static Task<Result<CampaignResponse>> PutMemberRole(this IWebAppClient client, Guid campaignId, Guid memberId, Role role)
        => client.Put<object, CampaignResponse>(new { role = role.ToString() }, $"/api/campaigns/{campaignId}/members/{memberId}/role");

    public static Task<Result<CampaignResponse>> GetCampaign(this IWebAppClient client, Guid campaignId)
        => client.Get<CampaignResponse>($"/api/campaigns/{campaignId}");

    public static Task<Result<GetCampaignsResponse>> GetCampaigns(this IWebAppClient client)
        => client.Get<GetCampaignsResponse>("/api/campaigns");

    private static Task<Result<TResponse>> Get<TResponse>(this IWebAppClient client, string url)
        => Result.Try(async () =>
            {
                var result = await client.AlbaHost.Scenario(_ =>
                {
                    _.Get.Url(url);
                    _.StatusCodeShouldBe(200);
                });
                return await result.ReadAsJsonAsync<TResponse>() ?? throw new InvalidCastException($"Could not cast response to type of {typeof(TResponse).Name}");
            });

    /// <summary>Sends a request and only checks the status code, for the failure paths.</summary>
    public static Task ExpectStatus(this IWebAppClient client, HttpMethod method, string url, object? body, int statusCode)
        => client.AlbaHost.Scenario(_ =>
        {
            if (method == HttpMethod.Get) _.Get.Url(url);
            else if (method == HttpMethod.Post) _.Post.Json(body ?? new { }).ToUrl(url);
            else if (method == HttpMethod.Put) _.Put.Json(body ?? new { }).ToUrl(url);
            else throw new NotSupportedException(method.ToString());
            _.StatusCodeShouldBe(statusCode);
        });
}
