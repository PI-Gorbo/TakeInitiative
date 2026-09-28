using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Search;

/// <summary>
/// Each SQL fragment in <see cref="SearchVisibilitySql"/> against the C# rule it is the twin of
/// (17a step 6, "Parity"), for every case: the set of ids the fragment accepts must equal the set
/// the rule accepts. It is the same job <c>SessionNoteAudienceTests</c> and
/// <c>EntryAudienceTests</c> do for the push side, and it needs Postgres, because the fragment is
/// SQL.
/// <para>
/// It is driven as a matrix, not as named one-offs: every combination of visibility, hiding,
/// ownership and role is a row, and one disagreement anywhere fails with the row that disagreed.
/// The documents are written straight to the tables, because the rules read document fields and
/// nothing here is about how a document came to be.
/// </para>
/// <para>
/// This is the suite that catches a drifted fragment. <see cref="SearchLeakTests"/> asks what a
/// viewer receives, and the providers re-run the C# rule on every row before building a response
/// (17a.6), so a fragment that let a row through would be dropped there and logged rather than
/// shown. The drift itself is only visible here.
/// </para>
/// </summary>
public class SearchVisibilityParityTests(WebAppWithDatabaseFixture fixture) : IClassFixture<WebAppWithDatabaseFixture>
{
    private IDocumentStore Store => fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>();

    /// <summary>The viewer, and the other member whose notes, entries and blocks they do not own.</summary>
    private static readonly Guid ViewerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Member Viewer(Role role) => new()
    {
        MemberId = ViewerId,
        UserId = Guid.NewGuid(),
        Role = role,
        JoinedAt = DateTimeOffset.UnixEpoch,
    };

    private static readonly Visibility[] Visibilities = Enum.GetValues<Visibility>();
    private static readonly bool[] Booleans = [false, true];

    public static TheoryData<Role> Roles => new() { Role.DM, Role.Player };

    /// <summary>
    /// The ids the fragment accepts. The campaign filter is in the same <c>WHERE</c>, as it is in
    /// every real query, and <c>@isDm</c> is passed only when the fragment reads it.
    /// </summary>
    private async Task<HashSet<Guid>> Accepted(string table, string fragment, Guid campaignId, Member viewer)
    {
        await using var session = Store.QuerySession();
        var rows = await SearchSql.QueryAsync(
            session,
            $"SELECT d.id FROM {SearchSql.Table(session, table)} d WHERE {SearchSql.CampaignFilter} AND {fragment}",
            command => Parameters(command, campaignId, viewer, fragment),
            reader => reader.GetGuid(0),
            default);
        return [.. rows];
    }

    private static void Parameters(NpgsqlCommand command, Guid campaignId, Member viewer, string fragment)
    {
        command.Parameters.AddWithValue("campaign", campaignId);
        command.Parameters.AddWithValue("me", SearchVisibilitySql.Me(viewer));
        if (fragment.Contains("@isDm"))
        {
            command.Parameters.AddWithValue("isDm", viewer.Role == Role.DM);
        }
    }

    // Notes: Everyone / DM / Me × hidden or not × the author or someone else, seen as a DM and as
    // a player. Those four viewer kinds are the step's "author, another DM, a player", because a
    // note whose author is not the viewer covers both of the last two.

    private static IEnumerable<SessionNote> Notes(Guid campaignId)
        => from visibility in Visibilities
           from hidden in Booleans
           from mine in Booleans
           select new SessionNote
           {
               Id = Guid.NewGuid(),
               CampaignId = campaignId,
               SessionId = Guid.NewGuid(),
               AuthorMemberId = mine ? ViewerId : OtherId,
               Text = $"{visibility}, hidden {hidden}, mine {mine}",
               Visibility = visibility,
               IsHidden = hidden,
               PostedAt = DateTimeOffset.UnixEpoch,
           };

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task TheNoteFragment_AcceptsExactlyWhatSessionNoteVisibilityDoes(Role role)
    {
        var campaignId = Guid.NewGuid();
        var notes = Notes(campaignId).ToList();
        notes.Should().HaveCount(Visibilities.Length * 2 * 2);
        await using (var session = Store.LightweightSession())
        {
            session.Store(notes.ToArray());
            await session.SaveChangesAsync();
        }

        var viewer = Viewer(role);
        var accepted = await Accepted(SearchSql.NoteTable, SearchVisibilitySql.Notes(viewer), campaignId, viewer);
        var canSee = notes.Where(n => SessionNoteVisibility.CanSee(n, viewer)).Select(n => n.Id).ToHashSet();

        Because(notes.ToDictionary(n => n.Id, n => n.Text), accepted, canSee, role);
    }

    // Entries: 3 visibilities × the creator or someone else × merged or not. A merged entry is out
    // of every list, which is what ListedEntries adds on top of the visibility rule.

    private static IEnumerable<Entry> Entries(Guid campaignId)
        => from visibility in Visibilities
           from mine in Booleans
           from merged in Booleans
           select new Entry
           {
               Id = Guid.NewGuid(),
               CampaignId = campaignId,
               CreatorMemberId = mine ? ViewerId : OtherId,
               Name = $"{visibility}, mine {mine}, merged {merged}",
               Kind = EntryKind.Character,
               Visibility = visibility,
               MergedIntoId = merged ? Guid.NewGuid() : null,
           };

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task TheEntryFragments_AcceptExactlyWhatEntryVisibilityDoes(Role role)
    {
        var campaignId = Guid.NewGuid();
        var entries = Entries(campaignId).ToList();
        entries.Should().HaveCount(Visibilities.Length * 2 * 2);
        await using (var session = Store.LightweightSession())
        {
            session.Store(entries.ToArray());
            await session.SaveChangesAsync();
        }

        var viewer = Viewer(role);
        var names = entries.ToDictionary(e => e.Id, e => e.Name);

        Because(
            names,
            await Accepted(SearchSql.EntryTable, SearchVisibilitySql.Entries(viewer), campaignId, viewer),
            entries.Where(e => EntryVisibility.CanSee(e, viewer)).Select(e => e.Id).ToHashSet(),
            $"{role}, the visibility rule");

        Because(
            names,
            await Accepted(SearchSql.EntryTable, SearchVisibilitySql.ListedEntries(viewer), campaignId, viewer),
            entries.Where(e => EntryVisibility.CanSee(e, viewer) && e.MergedIntoId is null).Select(e => e.Id).ToHashSet(),
            $"{role}, the listed rule");
    }

    // Blocks: the entry's visibility × the block's visibility × who owns each, seen as a DM and as
    // a player. The block fragment is only ever used inside ListedEntries, because the entry's own
    // audience applies as well, so that is how it is checked.

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task TheBlockFragment_AcceptsExactlyWhatCanSeeBlockDoes(Role role)
    {
        var campaignId = Guid.NewGuid();
        var entries = (
            from visibility in Visibilities
            from mine in Booleans
            select new Entry
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                CreatorMemberId = mine ? ViewerId : OtherId,
                Name = $"entry {visibility}, mine {mine}",
                Kind = EntryKind.Character,
                Visibility = visibility,
                Article = new Article
                {
                    Blocks =
                    [
                        .. from blockVisibility in Visibilities
                           from blockMine in Booleans
                           select new ArticleBlock
                           {
                               Id = Guid.NewGuid(),
                               Text = $"block {blockVisibility}, mine {blockMine}",
                               Visibility = blockVisibility,
                               OwnerMemberId = blockMine ? ViewerId : OtherId,
                           },
                    ],
                },
            }).ToList();

        var pairs = entries.SelectMany(e => e.Article.Blocks.Select(b => (Entry: e, Block: b))).ToList();
        pairs.Should().HaveCount(Visibilities.Length * 2 * Visibilities.Length * 2, "the whole matrix, 36 blocks");
        await using (var session = Store.LightweightSession())
        {
            session.Store(entries.ToArray());
            await session.SaveChangesAsync();
        }

        var viewer = Viewer(role);
        var fragment = $"{SearchVisibilitySql.ListedEntries(viewer)} AND {SearchVisibilitySql.Block}";
        await using var query = Store.QuerySession();
        var accepted = (await SearchSql.QueryAsync(
                query,
                $"""
                SELECT (b ->> 'Id')::uuid
                FROM {SearchSql.Table(query, SearchSql.EntryTable)} d
                CROSS JOIN LATERAL jsonb_array_elements(d.data -> 'Article' -> 'Blocks') AS b
                WHERE {SearchSql.CampaignFilter} AND {fragment}
                """,
                command => Parameters(command, campaignId, viewer, fragment),
                reader => reader.GetGuid(0),
                default))
            .ToHashSet();

        var canSee = pairs
            .Where(p => EntryVisibility.CanSeeBlock(p.Entry, p.Block, viewer))
            .Select(p => p.Block.Id)
            .ToHashSet();

        Because(
            pairs.ToDictionary(p => p.Block.Id, p => $"{p.Entry.Name} / {p.Block.Text}"),
            accepted, canSee, role);
    }

    // Combats (18f): started or not × each status, seen as a DM and as a player. A DM sees every
    // combat, so for a DM the fragment is meant to accept them all.

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task TheCombatFragment_AcceptsExactlyWhatCombatViewDoes(Role role)
    {
        var campaignId = Guid.NewGuid();
        var combats = (
            from status in Enum.GetValues<CombatStatus>()
            from started in Booleans
            select new Combat
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                SessionId = Guid.NewGuid(),
                Name = $"{status}, started {started}",
                Status = status,
                CreatedAt = DateTimeOffset.UnixEpoch,
                StartedAt = started ? DateTimeOffset.UnixEpoch : null,
            }).ToList();
        await using (var session = Store.LightweightSession())
        {
            session.Store(combats.ToArray());
            await session.SaveChangesAsync();
        }

        var viewer = Viewer(role);
        Because(
            combats.ToDictionary(c => c.Id, c => c.Name),
            await Accepted(SearchSql.CombatTable, SearchVisibilitySql.Combats(viewer), campaignId, viewer),
            combats.Where(c => CombatView.CanSee(c, viewer)).Select(c => c.Id).ToHashSet(),
            role,
            discriminates: role == Role.Player);
    }

    // Combatants: hidden or not × owned by the viewer, by someone else or by no one, inside a
    // started combat, the way the provider uses the fragment.

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task TheCombatantFragment_AcceptsExactlyWhatCombatViewDoes(Role role)
    {
        var campaignId = Guid.NewGuid();
        var combatants = (
            from hidden in Booleans
            from owner in new Guid?[] { ViewerId, OtherId, null }
            select new Combatant
            {
                Id = Guid.NewGuid(),
                Name = $"hidden {hidden}, owner {owner?.ToString()[..2] ?? "none"}",
                OwnerMemberId = owner,
                Hidden = hidden,
            }).ToList();
        var combat = new Combat
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            SessionId = Guid.NewGuid(),
            Name = "The combat",
            Status = CombatStatus.Active,
            CreatedAt = DateTimeOffset.UnixEpoch,
            StartedAt = DateTimeOffset.UnixEpoch,
            Combatants = combatants,
        };
        await using (var session = Store.LightweightSession())
        {
            session.Store(combat);
            await session.SaveChangesAsync();
        }

        var viewer = Viewer(role);
        var fragment = $"{SearchVisibilitySql.Combats(viewer)} AND {SearchVisibilitySql.Combatants(viewer)}";
        await using var query = Store.QuerySession();
        var accepted = (await SearchSql.QueryAsync(
                query,
                $"""
                SELECT (c ->> 'Id')::uuid
                FROM {SearchSql.Table(query, SearchSql.CombatTable)} d
                CROSS JOIN LATERAL jsonb_array_elements(d.data -> 'Combatants') AS c
                WHERE {SearchSql.CampaignFilter} AND {fragment}
                """,
                command => Parameters(command, campaignId, viewer, fragment),
                reader => reader.GetGuid(0),
                default))
            .ToHashSet();

        Because(
            combatants.ToDictionary(c => c.Id, c => c.Name),
            accepted,
            combatants.Where(c => CombatView.CanSee(c, viewer)).Select(c => c.Id).ToHashSet(),
            role,
            discriminates: role == Role.Player);
    }

    /// <summary>
    /// The two sets, compared by what each row <b>is</b> rather than by its id, so a failure names
    /// the case that disagreed: a row SQL let through that C# would not is a leak, and a row C#
    /// allows that SQL drops is a hidden result.
    /// </summary>
    /// <param name="discriminates">
    /// Whether the rule keeps something out for this viewer. A DM sees every combat and combatant,
    /// so for them the combat fragments accept everything by design.
    /// </param>
    private static void Because<T>(
        IReadOnlyDictionary<Guid, string> labels, HashSet<Guid> accepted, HashSet<Guid> canSee, T role, bool discriminates = true)
    {
        string[] Names(IEnumerable<Guid> ids) => [.. ids.Select(id => labels[id]).OrderBy(x => x)];

        // The fragment must discriminate, or the comparison below would hold for a WHERE false and
        // for a WHERE true alike.
        accepted.Should().NotBeEmpty($"the fragment lets something through ({role})");
        if (discriminates)
        {
            accepted.Count.Should().BeLessThan(labels.Count, $"the fragment keeps something out ({role})");
        }

        Names(accepted.Except(canSee)).Should().BeEmpty($"SQL must not accept what the C# rule denies ({role})");
        Names(canSee.Except(accepted)).Should().BeEmpty($"SQL must accept everything the C# rule allows ({role})");
        Names(accepted).Should().Equal(Names(canSee));
    }
}
