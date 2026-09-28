using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.LooseEnds;
using TakeInitiative.Api.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.LooseEnds;

/// <summary>Calls and seeds for the loose-end tests (19b). Each call keeps the raw body, so the leak tests can search it.</summary>
public static class LooseEndTestKit
{
    public static string LooseEndsUrl(Guid campaignId) => $"/api/campaigns/{campaignId}/loose-ends";

    public static async Task<Reply> LooseEnds(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, Guid? sessionId = null)
    {
        fixture.LoginAsUser(who);
        return await fixture.Call(HttpMethod.Get, LooseEndsUrl(campaignId) + (sessionId is null ? "" : $"?sessionId={sessionId}"));
    }

    public static async Task<LooseEndResponse[]> LooseEndsOf(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, Guid? sessionId = null)
        => (await fixture.LooseEnds(who, campaignId, sessionId).Ok()).As<LooseEndsResponse>().Items;

    public static async Task<Reply> LooseEndCounts(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId)
    {
        fixture.LoginAsUser(who);
        return await fixture.Call(HttpMethod.Get, LooseEndsUrl(campaignId) + "/counts");
    }

    public static async Task<LooseEndCountsResponse> LooseEndCountsOf(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId)
        => (await fixture.LooseEndCounts(who, campaignId).Ok()).As<LooseEndCountsResponse>();

    /// <summary>A note with one image and this caption, as <paramref name="who"/>.</summary>
    public static async Task<SessionNoteResponse> ImageNote(
        this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, string caption = "", Visibility visibility = Visibility.Everyone)
    {
        fixture.LoginAsUser(who);
        var image = await fixture.UploadFixture(campaignId, ImageFixtures.Webp);
        var note = await fixture.PostImageNote(campaignId, caption, [image.Id], visibility);
        note.Should().Succeed();
        return note.Value;
    }

    /// <summary>The loose ends as (kind, note or entry id) pairs, in order.</summary>
    public static (LooseEndKind Kind, Guid Id)[] Keys(this IEnumerable<LooseEndResponse> items)
        => items.Select(i => (i.Kind, i.Note?.Id ?? i.Entry!.Id)).ToArray();
}
