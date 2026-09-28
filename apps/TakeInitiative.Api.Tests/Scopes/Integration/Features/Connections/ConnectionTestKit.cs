using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Connections;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Tests.Integration.Features.Combats;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.Connections;

/// <summary>Calls and seeds for the connection tests (19a). Each call keeps the raw body, so the leak tests can search it.</summary>
public static class ConnectionTestKit
{
    public static string Mention(EntryResponse entry) => $"@[{entry.Name}](entry:{entry.Id})";

    public static string ConnectionsUrl(Guid campaignId, Guid entryId) => $"/api/campaigns/{campaignId}/entries/{entryId}/connections";

    public static async Task<Reply> Connections(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(who);
        return await fixture.Call(HttpMethod.Get, ConnectionsUrl(campaignId, entryId));
    }

    public static async Task<EntryConnectionResponse[]> ConnectionsOf(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, Guid entryId)
        => (await fixture.Connections(who, campaignId, entryId).Ok()).As<EntryConnectionsResponse>().Connections;

    public static async Task<Reply> Evidence(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, Guid entryId, Guid otherId)
    {
        fixture.LoginAsUser(who);
        return await fixture.Call(HttpMethod.Get, $"{ConnectionsUrl(campaignId, entryId)}/{otherId}");
    }

    public static async Task<ConnectionEvidenceResponse> EvidenceOf(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, Guid entryId, Guid otherId)
        => (await fixture.Evidence(who, campaignId, entryId, otherId).Ok()).As<ConnectionEvidenceResponse>();

    public static async Task<Reply> Graph(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, string query = "")
    {
        fixture.LoginAsUser(who);
        return await fixture.Call(HttpMethod.Get, $"/api/campaigns/{campaignId}/connections/graph{query}");
    }

    public static async Task<ConnectionGraphResponse> GraphOf(this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, string query = "")
        => (await fixture.Graph(who, campaignId, query).Ok()).As<ConnectionGraphResponse>();

    public static async Task<SessionNoteResponse> Note(
        this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, string text, Visibility visibility = Visibility.Everyone)
    {
        fixture.LoginAsUser(who);
        var note = await fixture.PostSessionNote(campaignId, text, visibility);
        note.Should().Succeed();
        return note.Value;
    }

    /// <summary>Replaces an entry's article with these blocks, as <paramref name="who"/>.</summary>
    public static async Task<EntryResponse> Article(
        this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, Guid entryId, params WebAppClientExtensions.BlockEdit[] blocks)
    {
        fixture.LoginAsUser(who);
        var view = (await fixture.GetEntry(campaignId, entryId)).Value;
        var saved = await fixture.PutEntryArticle(campaignId, entryId, view.Article.Etag, blocks);
        saved.Should().Succeed();
        return saved.Value;
    }

    /// <summary>A started combat with these combatants (<c>new { entryId, hidden }</c> objects).</summary>
    public static async Task<Guid> StartedCombat(this AuthenticatedWebAppWithDatabaseFixture fixture, Guid campaignId, string name, params object[] combatants)
    {
        var combat = await fixture.CreateCombat(campaignId, name);
        await fixture.AddAsDm(campaignId, combat.Id, combatants);
        fixture.LoginAsUser(Users.DM);
        await fixture.Roll(campaignId, combat.Id).Ok();
        return combat.Id;
    }

    /// <summary>A Draft combat with these combatants.</summary>
    public static async Task<Guid> DraftCombat(this AuthenticatedWebAppWithDatabaseFixture fixture, Guid campaignId, string name, params object[] combatants)
    {
        var combat = await fixture.CreateCombat(campaignId, name);
        await fixture.AddAsDm(campaignId, combat.Id, combatants);
        return combat.Id;
    }

    public static EntryConnectionResponse? With(this IEnumerable<EntryConnectionResponse> connections, EntryResponse other)
        => connections.SingleOrDefault(c => c.Entry.Id == other.Id);
}
