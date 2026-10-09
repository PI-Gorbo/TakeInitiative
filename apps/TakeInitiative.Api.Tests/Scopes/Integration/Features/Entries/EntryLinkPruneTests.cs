using FluentAssertions;

using Npgsql;

using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Tests.Integration.Features.Reference;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using TakeInitiative.KnowledgeBase.Store;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// 26c's prune protection, with a real link (27b). <c>KnowledgeBaseStoreTests</c> pins the question
/// the store asks — one call, this provider, exactly the candidate ids, on the prune's own connection
/// and transaction — against a stub, because when it was written no entry could link to anything.
/// This is the other half: the same <see cref="KnowledgeBaseStore.PruneAsync" />, answered by the
/// real <see cref="EntryKnowledgeBaseLinks" />, reading an entry that a real
/// <c>POST links</c> wrote.
/// </summary>
/// <remarks>
/// It prunes the fixture's corpus, which is why it has a fixture of its own rather than sharing one:
/// a prune deletes rows, and the other knowledge-base suites assert against all sixteen of them.
/// </remarks>
public class EntryLinkPruneTests(KnowledgeBaseFixture fixture) : IClassFixture<KnowledgeBaseFixture>
{
    private const string Linked = "monster_test-gremlin_tst";
    private const string Unlinked = "monster_test-gremlin-chief_tst";
    private const string Provider = KnowledgeBaseCorpus.Provider;

    [Fact]
    public async Task ThePruneNamesTheEntryTableTheApiActuallyWrites()
    {
        // The two names EntryKnowledgeBaseLinks hard-codes about the API, pinned here so that
        // renaming the property or the table fails a test rather than silently unprotecting every
        // link in the database.
        EntryKnowledgeBaseLinks.EntryTable.Should().Be(SearchSql.EntryTable);
        EntryKnowledgeBaseLinks.ItemKeysField.Should().Be(nameof(Entry.LinkedItemKeys));

        // And that the GIN index the `?|` needs is there, spelled the way the statement spells it.
        // Without it the prune scans every entry in every campaign.
        await using var connection = new NpgsqlConnection(fixture.PostgreSqlContainer.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"select indexdef from pg_indexes where tablename = '{SearchSql.EntryTable}' and indexdef like '%{nameof(Entry.LinkedItemKeys)}%'";
        var indexDef = (string?)await command.ExecuteScalarAsync();

        indexDef.Should().NotBeNull().And.Contain("USING gin");
        indexDef.Should().Contain($"data ->> '{nameof(Entry.LinkedItemKeys)}'");
    }

    [Fact]
    public async Task ALinkedRowSurvivesAPrune_AsStale_AndResolvesThatWay_WhileAnUnlinkedOneIsDeleted()
    {
        var campaign = await TestCampaign.Create(fixture, "Prune: a real link");
        fixture.LoginAsUser(Users.DM);
        var caves = await fixture.PostEntry(campaign.Id, "The Lint Caves", EntryKind.Place);
        caves.Should().Succeed();
        var linked = await fixture.PostKnowledgeBaseLink(campaign.Id, caves.Value.Id, Provider, Linked);
        linked.Should().Succeed();
        linked.Value.Links.Should().ContainSingle().Which.Stale.Should().BeFalse();

        // The ingest's own store, with the ingest's own link reader, pruning a source that has lost
        // both rows. --force, so the threshold is not what is being tested.
        var store = new KnowledgeBaseStore(
            fixture.PostgreSqlContainer.GetConnectionString(),
            new EntryKnowledgeBaseLinks());
        var keep = KnowledgeBaseCorpus.Rows
            .Select(row => row.Id)
            .Where(id => id != Linked && id != Unlinked)
            .ToList();

        var prune = await store.PruneAsync(Provider, keep, force: true);

        prune.Considered.Should().Be(2);
        prune.MarkedStale.Should().Be(1, "the linked row is kept");
        prune.Deleted.Should().Be(1, "the unlinked one is not");

        var stored = await store.ReadAllAsync(Provider);
        stored.Should().ContainSingle(row => row.Id == Linked).Which.Stale.Should().BeTrue();
        stored.Should().NotContain(row => row.Id == Unlinked);

        // And the link now reads as "no longer in your knowledge base": present, named, removable,
        // with nowhere to go.
        fixture.LoginAsUser(Users.DM);
        var after = (await fixture.GetEntry(campaign.Id, caves.Value.Id)).Value.Links.Should().ContainSingle().Subject;
        after.Stale.Should().BeTrue();
        after.Url.Should().BeNull("there is nowhere honest to send the reader any more");
        after.ItemId.Should().Be(Linked);
        after.Name.Should().Be("Test Gremlin", "the last-known name is what lets the member recognise which link went");

        // Removing it still works, and the row it protected stays staled rather than coming back.
        (await fixture.DeleteEntryLink(campaign.Id, caves.Value.Id, after.Id)).Value.Links.Should().BeEmpty();
    }
}
