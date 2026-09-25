using CSharpFunctionalExtensions;
using Microsoft.Extensions.Primitives;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Images;
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
        Visibility visibility = Visibility.Everyone, bool isRecap = false, Guid? sessionId = null,
        params NewEntry[] newEntries)
        => client.Post<object, SessionNoteResponse>(
            new { sessionId, text, visibility = visibility.ToString(), isRecap, newEntries = NewEntriesBody(newEntries) },
            $"/api/campaigns/{campaignId}/notes");

    /// <summary>An entry to create with a note (step 15b): the id must be mentioned in the text.</summary>
    public record NewEntry(Guid Id, string Name, EntryKind Kind = EntryKind.Character)
    {
        /// <summary>The stored mention of this entry, <c>@[name](entry:id)</c>.</summary>
        public string Mention => $"@[{Name}](entry:{Id})";
    }

    private static object[]? NewEntriesBody(NewEntry[] newEntries)
        => newEntries.Length == 0 ? null : newEntries.Select(e => (object)new { id = e.Id, name = e.Name, kind = e.Kind.ToString() }).ToArray();

    public static Task<Result<GetSessionNoteResponse>> GetSessionNote(this IWebAppClient client, Guid campaignId, Guid noteId)
        => client.Get<GetSessionNoteResponse>($"/api/campaigns/{campaignId}/notes/{noteId}");

    public static Task<Result<SessionNoteResponse>> PutSessionNote(
        this IWebAppClient client, Guid campaignId, Guid noteId, string text, bool isRecap = false, params NewEntry[] newEntries)
        => client.Put<object, SessionNoteResponse>(
            new { text, isRecap, newEntries = NewEntriesBody(newEntries) }, $"/api/campaigns/{campaignId}/notes/{noteId}");

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

    // Entries (step 15a).

    public static Task<Result<GetEntriesResponse>> GetEntries(this IWebAppClient client, Guid campaignId)
        => client.Get<GetEntriesResponse>($"/api/campaigns/{campaignId}/entries");

    public static Task<Result<EntryResponse>> PostEntry(
        this IWebAppClient client, Guid campaignId, string name, EntryKind kind = EntryKind.Character, Visibility visibility = Visibility.Everyone)
        => client.Post<object, EntryResponse>(
            new { name, kind = kind.ToString(), visibility = visibility.ToString() },
            $"/api/campaigns/{campaignId}/entries");

    public static Task<Result<EntryResponse>> GetEntry(this IWebAppClient client, Guid campaignId, Guid entryId)
        => client.Get<EntryResponse>($"/api/campaigns/{campaignId}/entries/{entryId}");

    public static Task<Result<EntryResponse>> PutEntryName(this IWebAppClient client, Guid campaignId, Guid entryId, string name)
        => client.Put<object, EntryResponse>(new { name }, $"/api/campaigns/{campaignId}/entries/{entryId}/name");

    public static Task<Result<EntryResponse>> PutEntryKind(this IWebAppClient client, Guid campaignId, Guid entryId, EntryKind kind)
        => client.Put<object, EntryResponse>(new { kind = kind.ToString() }, $"/api/campaigns/{campaignId}/entries/{entryId}/kind");

    public static Task<Result<EntryResponse>> PutEntryAliases(this IWebAppClient client, Guid campaignId, Guid entryId, params string[] aliases)
        => client.Put<object, EntryResponse>(new { aliases }, $"/api/campaigns/{campaignId}/entries/{entryId}/aliases");

    public static Task<Result<EntryResponse>> PutEntryVisibility(this IWebAppClient client, Guid campaignId, Guid entryId, Visibility visibility)
        => client.Put<object, EntryResponse>(new { visibility = visibility.ToString() }, $"/api/campaigns/{campaignId}/entries/{entryId}/visibility");

    public static Task<Result<EntryResponse>> PutEntryEditAccess(this IWebAppClient client, Guid campaignId, Guid entryId, EditAccess editAccess)
        => client.Put<object, EntryResponse>(new { editAccess = editAccess.ToString() }, $"/api/campaigns/{campaignId}/entries/{entryId}/edit-access");

    // Mentions and timelines (step 15b).

    public static Task<Result<EntryTimelineResponse>> GetEntryTimeline(
        this IWebAppClient client, Guid campaignId, Guid entryId, DateTimeOffset? before = null, int? take = null)
    {
        var query = new List<string>();
        if (before is not null) query.Add($"before={Uri.EscapeDataString(before.Value.ToString("O"))}");
        if (take is not null) query.Add($"take={take}");
        var url = $"/api/campaigns/{campaignId}/entries/{entryId}/timeline" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return client.Get<EntryTimelineResponse>(url);
    }

    // Articles, secret blocks and promote (step 15e).

    /// <summary>One block of a <c>PUT article</c> body: an existing block's id, or null for a new one.</summary>
    public record BlockEdit(Guid? Id, string Text, Visibility Visibility = Visibility.Everyone)
    {
        public static BlockEdit Keep(ArticleBlockResponse block) => new(block.Id, block.Text, block.Visibility);
        public static BlockEdit Change(ArticleBlockResponse block, string text) => new(block.Id, text, block.Visibility);
    }

    public static object ArticleBody(string etag, IEnumerable<BlockEdit> blocks, params NewEntry[] newEntries) => new
    {
        etag,
        blocks = blocks.Select(b => new { id = b.Id, text = b.Text, visibility = b.Visibility.ToString() }).ToArray(),
        newEntries = NewEntriesBody(newEntries),
    };

    public static string ArticleUrl(Guid campaignId, Guid entryId) => $"/api/campaigns/{campaignId}/entries/{entryId}/article";

    public static string QuotesUrl(Guid campaignId, Guid entryId) => $"/api/campaigns/{campaignId}/entries/{entryId}/quotes";

    public static Task<Result<EntryResponse>> PutEntryArticle(
        this IWebAppClient client, Guid campaignId, Guid entryId, string etag, IEnumerable<BlockEdit> blocks, params NewEntry[] newEntries)
        => client.Put<object, EntryResponse>(ArticleBody(etag, blocks, newEntries), ArticleUrl(campaignId, entryId));

    public static Task<Result<EntryQuoteResponse>> PostEntryQuote(this IWebAppClient client, Guid campaignId, Guid entryId, Guid noteId, string? text = null)
        => client.Post<object, EntryQuoteResponse>(new { noteId, text }, QuotesUrl(campaignId, entryId));

    // Merge, claim, stats and history (step 15g).

    public static string EntryUrl(Guid campaignId, Guid entryId, string? part = null)
        => $"/api/campaigns/{campaignId}/entries/{entryId}" + (part is null ? "" : $"/{part}");

    public static Task<Result<EntryResponse>> PostEntryMerge(this IWebAppClient client, Guid campaignId, Guid entryId, Guid intoEntryId)
        => client.Post<object, EntryResponse>(new { intoEntryId }, EntryUrl(campaignId, entryId, "merge"));

    public static Task<Result<EntryResponse>> PutEntryClaim(this IWebAppClient client, Guid campaignId, Guid entryId, Guid? memberId)
        => client.Put<object, EntryResponse>(new { memberId }, EntryUrl(campaignId, entryId, "claim"));

    public static Task<Result<EntryResponse>> PutEntryStats(
        this IWebAppClient client, Guid campaignId, Guid entryId, string? initiativeRoll, string? maxHp, int? ac)
        => client.Put<object, EntryResponse>(new { initiativeRoll, maxHp, ac }, EntryUrl(campaignId, entryId, "stats"));

    public static Task<Result<EntryHistoryResponse>> GetEntryHistory(this IWebAppClient client, Guid campaignId, Guid entryId)
        => client.Get<EntryHistoryResponse>(EntryUrl(campaignId, entryId, "history"));

    // Images (step 16a).

    public static string ImagesUrl(Guid campaignId) => $"/api/campaigns/{campaignId}/images";

    public static string ImageUrl(Guid campaignId, Guid imageId, string? variant = null)
        => $"/api/campaigns/{campaignId}/images/{imageId}" + (variant is null ? "" : $"/{variant}");

    /// <summary>Uploads <paramref name="bytes"/> as the <c>file</c> part and returns the status, the body and, on a 200, the image.</summary>
    public static async Task<(int Status, string Body, ImageResponse? Image)> UploadImage(
        this IWebAppClient client, Guid campaignId, byte[] bytes, string fileName = "photo.jpg", string contentType = "image/jpeg")
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        var result = await client.AlbaHost.Scenario(_ =>
        {
            _.Post.MultipartFormData(content).ToUrl(ImagesUrl(campaignId));
            _.IgnoreStatusCode();
        });
        var status = result.Context.Response.StatusCode;
        var body = await result.ReadAsTextAsync();
        var image = status == 200
            ? System.Text.Json.JsonSerializer.Deserialize<ImageResponse>(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
            : null;
        return (status, body, image);
    }

    /// <summary>Uploads a fixture from <c>Fixtures/images</c> and expects a 200.</summary>
    public static async Task<ImageResponse> UploadFixture(this IWebAppClient client, Guid campaignId, string fixture)
    {
        var (status, body, image) = await client.UploadImage(campaignId, ImageFixtures.Bytes(fixture), fixture);
        if (status != 200 || image is null)
        {
            throw new InvalidOperationException($"Uploading {fixture} gave {status}: {body}");
        }
        return image;
    }

    /// <summary>GETs a variant's bytes, never asserting the status.</summary>
    public static Task<Alba.IScenarioResult> GetImageVariant(
        this IWebAppClient client, Guid campaignId, Guid imageId, string variant, string? ifNoneMatch = null)
        => client.AlbaHost.Scenario(_ =>
        {
            _.Get.Url(ImageUrl(campaignId, imageId, variant));
            if (ifNoneMatch is not null) _.WithRequestHeader("If-None-Match", ifNoneMatch);
            _.IgnoreStatusCode();
        });

    public static async Task<int> DeleteImage(this IWebAppClient client, Guid campaignId, Guid imageId)
    {
        var result = await client.AlbaHost.Scenario(_ =>
        {
            _.Delete.Url(ImageUrl(campaignId, imageId));
            _.IgnoreStatusCode();
        });
        return result.Context.Response.StatusCode;
    }

    /// <summary>Posts a note with images (step 16b). The text may be empty.</summary>
    public static Task<Result<SessionNoteResponse>> PostImageNote(
        this IWebAppClient client, Guid campaignId, string text, Guid[] imageIds, Visibility visibility = Visibility.Everyone)
        => client.Post<object, SessionNoteResponse>(
            new { text, visibility = visibility.ToString(), isRecap = false, imageIds }, $"/api/campaigns/{campaignId}/notes");

    /// <summary>Edits a note with an image list (step 16b): null leaves the images alone.</summary>
    public static Task<Result<SessionNoteResponse>> PutImageNote(
        this IWebAppClient client, Guid campaignId, Guid noteId, string text, Guid[]? imageIds, bool isRecap = false)
        => client.Put<object, SessionNoteResponse>(new { text, isRecap, imageIds }, $"/api/campaigns/{campaignId}/notes/{noteId}");

    /// <summary>Sends a request that should fail and returns its status and body, to check error keys and that nothing leaks.</summary>
    public static async Task<(int Status, string Body)> Send(this IWebAppClient client, HttpMethod method, string url, object body)
    {
        var result = await client.AlbaHost.Scenario(_ =>
        {
            if (method == HttpMethod.Post) _.Post.Json(body).ToUrl(url);
            else if (method == HttpMethod.Put) _.Put.Json(body).ToUrl(url);
            else throw new NotSupportedException(method.ToString());
            _.IgnoreStatusCode();
        });
        return (result.Context.Response.StatusCode, await result.ReadAsTextAsync());
    }

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
