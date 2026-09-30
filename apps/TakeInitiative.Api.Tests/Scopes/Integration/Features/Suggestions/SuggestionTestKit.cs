using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Features.Suggestions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.Suggestions;

/// <summary>Calls for the suggestion tests (23c). Each keeps the raw body, so the leak tests can search it.</summary>
public static class SuggestionTestKit
{
    public const string TestModel = "gliner_small-v2.5";
    public const string TestVersion = "gliner-community/gliner_small-v2.5@f227d3cd+onnx-int8";

    public static string SuggestionsUrl(Guid campaignId, string part) => $"/api/campaigns/{campaignId}/suggestions/{part}";

    public static async Task<Reply> Match(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, params string[] spans)
    {
        fixture.LoginAsUser(who);
        return await fixture.Call(HttpMethod.Post, SuggestionsUrl(campaignId, "match"),
            new { spans = spans.Select(s => new { text = s, kind = "Character" }).ToArray() });
    }

    public static async Task<SuggestionMatchResponse?[]> MatchesOf(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, params string[] spans)
        => (await fixture.Match(who, campaignId, spans).Ok()).As<PostSuggestionMatchResponse>().Matches;

    /// <summary>The span <paramref name="span"/> (its first occurrence) of <paramref name="text"/>, linked to <paramref name="entryId"/>.</summary>
    public static (string Text, int Start, int Length) Linked(string text, string span, Guid entryId, int from = 0)
    {
        var start = text.IndexOf(span, from, StringComparison.Ordinal);
        return (text[..start] + SuggestionEdit.MentionOf(span, entryId) + text[(start + span.Length)..], start, span.Length);
    }

    /// <summary>A <c>PUT notes/{id}</c> accepting a suggestion, as <paramref name="who"/>.</summary>
    public static async Task<Reply> Accept(
        this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, Guid noteId, string newText,
        int start, int length, Guid entryId, double confidence = 0.87, string version = TestVersion, object[]? newEntries = null,
        bool isRecap = false)
    {
        fixture.LoginAsUser(who);
        return await fixture.Call(HttpMethod.Put, $"/api/campaigns/{campaignId}/notes/{noteId}", new
        {
            text = newText,
            isRecap,
            newEntries,
            suggestion = new { model = TestModel, version, confidence, start, length, entryId },
        });
    }

    /// <summary>Accepts the suggestion linking <paramref name="span"/> of <paramref name="note"/> to <paramref name="entryId"/>; the edited note comes back.</summary>
    public static async Task<SessionNoteResponse> Accepted(
        this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, SessionNoteResponse note, string span, Guid entryId,
        string version = TestVersion)
    {
        var (text, start, length) = Linked(note.Text, span, entryId);
        return (await fixture.Accept(who, campaignId, note.Id, text, start, length, entryId, version: version).Ok()).As<SessionNoteResponse>();
    }

    public static async Task<Reply> Models(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId)
    {
        fixture.LoginAsUser(who);
        return await fixture.Call(HttpMethod.Get, SuggestionsUrl(campaignId, "models"));
    }

    public static async Task<SuggestionModelResponse[]> ModelsOf(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId)
        => (await fixture.Models(who, campaignId).Ok()).As<GetSuggestionModelsResponse>().Models;

    public static async Task<Reply> Revert(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, string version = TestVersion)
    {
        fixture.LoginAsUser(who);
        return await fixture.Call(HttpMethod.Post, SuggestionsUrl(campaignId, "revert"), new { model = TestModel, version });
    }

    public static async Task<PostSuggestionRevertResponse> Reverted(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, string version = TestVersion)
        => (await fixture.Revert(who, campaignId, version).Ok()).As<PostSuggestionRevertResponse>();
}
