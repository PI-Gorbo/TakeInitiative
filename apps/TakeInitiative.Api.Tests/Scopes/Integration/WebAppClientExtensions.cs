using CSharpFunctionalExtensions;
using Microsoft.Extensions.Primitives;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Sessions;
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

    // Sessions and session notes (step 14a).

    public static Task<Result<GetSessionsResponse>> GetSessions(this IWebAppClient client, Guid campaignId)
        => client.Get<GetSessionsResponse>($"/api/campaigns/{campaignId}/sessions");

    public static Task<Result<SessionResponse>> PostStartSession(this IWebAppClient client, Guid campaignId, int number)
        => client.Post<object, SessionResponse>(new { number }, $"/api/campaigns/{campaignId}/sessions");

    public static Task<Result<SessionResponse>> PutSessionTitle(this IWebAppClient client, Guid campaignId, Guid sessionId, string? title)
        => client.Put<object, SessionResponse>(new { title }, $"/api/campaigns/{campaignId}/sessions/{sessionId}/title");

    public static Task<Result<SessionStreamResponse>> GetSessionStream(
        this IWebAppClient client, Guid campaignId, SessionStreamFilter? filter = null, int? before = null, int? take = null)
    {
        var query = new List<string>();
        if (filter is not null) query.Add($"filter={filter}");
        if (before is not null) query.Add($"before={before}");
        if (take is not null) query.Add($"take={take}");
        var url = $"/api/campaigns/{campaignId}/stream" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return client.Get<SessionStreamResponse>(url);
    }

    public static Task<Result<SessionNoteResponse>> PostSessionNote(
        this IWebAppClient client, Guid campaignId, string text,
        Visibility visibility = Visibility.Everyone, bool isRecap = false, Guid? sessionId = null)
        => client.Post<object, SessionNoteResponse>(
            new { sessionId, text, visibility = visibility.ToString(), isRecap },
            $"/api/campaigns/{campaignId}/notes");

    public static Task<Result<GetSessionNoteResponse>> GetSessionNote(this IWebAppClient client, Guid campaignId, Guid noteId)
        => client.Get<GetSessionNoteResponse>($"/api/campaigns/{campaignId}/notes/{noteId}");

    public static Task<Result<SessionNoteResponse>> PutSessionNote(this IWebAppClient client, Guid campaignId, Guid noteId, string text, bool isRecap = false)
        => client.Put<object, SessionNoteResponse>(new { text, isRecap }, $"/api/campaigns/{campaignId}/notes/{noteId}");

    public static Task<Result<SessionNoteResponse>> PutSessionNoteVisibility(this IWebAppClient client, Guid campaignId, Guid noteId, Visibility visibility)
        => client.Put<object, SessionNoteResponse>(new { visibility = visibility.ToString() }, $"/api/campaigns/{campaignId}/notes/{noteId}/visibility");

    public static Task<Result<SessionNoteResponse>> PutSessionNoteHidden(this IWebAppClient client, Guid campaignId, Guid noteId, bool hidden)
        => client.Put<object, SessionNoteResponse>(new { hidden }, $"/api/campaigns/{campaignId}/notes/{noteId}/hidden");

    public static Task<Result> DeleteSessionNote(this IWebAppClient client, Guid campaignId, Guid noteId)
        => Result.Try(async () =>
            {
                await client.AlbaHost.Scenario(_ =>
                {
                    _.Delete.Url($"/api/campaigns/{campaignId}/notes/{noteId}");
                    _.StatusCodeShouldBe(204);
                });
            });

    public static Task<Result<GetSessionNoteHistoryResponse>> GetSessionNoteHistory(this IWebAppClient client, Guid campaignId, Guid noteId)
        => client.Get<GetSessionNoteHistoryResponse>($"/api/campaigns/{campaignId}/notes/{noteId}/history");

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
            else if (method == HttpMethod.Delete) _.Delete.Url(url);
            else throw new NotSupportedException(method.ToString());
            _.StatusCodeShouldBe(statusCode);
        });
}
