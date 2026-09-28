using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Features.Connections;
using TakeInitiative.Api.Features.Entries;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// What counts as a connection (19a.1): pairs per source, once per source, merges, no
/// self-pairs, the article's own entry joining its blocks, unknown ids dropped, combats through
/// visible combatants only. And the graph's shape and cap (19a.5).
/// </summary>
public class ConnectionPairsTests
{
    private static readonly Member Dm = new() { MemberId = Guid.NewGuid(), UserId = Guid.NewGuid(), Role = Role.DM, JoinedAt = default };
    private static readonly Member Player = new() { MemberId = Guid.NewGuid(), UserId = Guid.NewGuid(), Role = Role.Player, JoinedAt = default };

    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();

    private static ConnectionSource Note(params Guid[] ids) => new(EvidenceKind.Note, Guid.NewGuid(), ids, DateTimeOffset.UnixEpoch);

    private static IReadOnlyDictionary<EntryPair, Connection> Pairs(IEnumerable<ConnectionSource> sources, IReadOnlyDictionary<Guid, Guid>? merges = null, params Guid[] visible)
        => ConnectionPairs.From(sources, merges ?? new Dictionary<Guid, Guid>(), (visible.Length == 0 ? [A, B, C] : visible).ToHashSet());

    private static Entry Entry(string name, EntryKind kind = EntryKind.Character, params ArticleBlock[] blocks) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = kind,
        Visibility = Visibility.Everyone,
        CreatorMemberId = Dm.MemberId,
        Article = new Article { Blocks = blocks },
        ArticleMentionIds = blocks.SelectMany(b => MentionParser.EntryIds(b.Text)).Distinct().ToArray(),
    };

    private static ArticleBlock Block(string text, Visibility visibility = Visibility.Everyone)
        => new() { Id = Guid.NewGuid(), Text = text, Visibility = visibility, OwnerMemberId = Dm.MemberId };

    private static string Mention(Entry e) => $"@[{e.Name}](entry:{e.Id})";

    [Fact]
    public void ANoteWithThreeEntries_GivesEachOfItsThreePairsOnePiece()
    {
        var pairs = Pairs([Note(A, B, C)]);
        pairs.Keys.Should().BeEquivalentTo([new EntryPair(A, B), new EntryPair(A, C), new EntryPair(B, C)]);
        pairs.Values.Should().OnlyContain(c => c.Weight == 1);
    }

    [Fact]
    public void ASource_CountsOncePerPair_HoweverOftenItRepeats_AndPairsAreUndirected()
    {
        var pairs = Pairs([Note(A, B, A, B), Note(B, A)]);
        pairs.Should().ContainSingle();
        pairs[new EntryPair(B, A)].Weight.Should().Be(2);
        new EntryPair(A, B).Should().Be(new EntryPair(B, A));
    }

    [Fact]
    public void AMergedId_MeansItsTarget_AndNeverPairsWithIt()
    {
        var loser = Guid.NewGuid();
        var merges = new Dictionary<Guid, Guid> { [loser] = A };
        var pairs = Pairs([Note(loser, A), Note(loser, B)], merges);
        pairs.Keys.Should().Equal(new EntryPair(A, B));
    }

    [Fact]
    public void IdsTheViewerCannotSee_AreDropped()
    {
        var unknown = Guid.NewGuid();
        Pairs([Note(A, unknown)]).Should().BeEmpty();
        Pairs([Note(A, B, unknown)]).Keys.Should().Equal(new EntryPair(A, B));
    }

    [Fact]
    public void AnArticlesOwnEntry_JoinsEachVisibleBlock_ButBlocksDoNotJoinEachOther()
    {
        var tharden = Entry("Tharden");
        var phandalin = Entry("Phandalin", EntryKind.Place);
        var klarg = Entry("Klarg");
        var gundren = Entry("Gundren", EntryKind.Character,
            Block($"Brother of {Mention(tharden)}, {Mention(tharden)}."),
            Block($"Lives in {Mention(phandalin)}."),
            Block($"Hunted by {Mention(klarg)}.", Visibility.DM));
        var entries = new[] { gundren, tharden, phandalin, klarg };

        var player = ConnectionIndex.Pair(entries, [], [gundren], [], Player).Pairs;
        player.Keys.Should().BeEquivalentTo([new EntryPair(gundren.Id, tharden.Id), new EntryPair(gundren.Id, phandalin.Id)]);
        player.Values.Should().OnlyContain(c => c.Weight == 1 && c.Blocks == 1 && c.LastAt == null);
        player[new EntryPair(gundren.Id, tharden.Id)].Evidence.Single().ArticleEntryId.Should().Be(gundren.Id);

        ConnectionIndex.Pair(entries, [], [gundren], [], Dm).Pairs.Keys
            .Should().Contain(new EntryPair(gundren.Id, klarg.Id), "the DM sees the secret block");
    }

    [Fact]
    public void Combats_CountThroughVisibleCombatants_OnceStarted()
    {
        var brynn = Entry("Brynn");
        var goblin = Entry("Goblin");
        var wolf = Entry("Wolf");
        var entries = new[] { brynn, goblin, wolf };
        Combatant Of(Entry e, bool hidden = false) => new() { Id = Guid.NewGuid(), Name = e.Name, EntryId = e.Id, Hidden = hidden };
        Combat Fight(CombatStatus status, DateTimeOffset? startedAt, params Combatant[] combatants) => new()
        {
            Id = Guid.NewGuid(),
            Status = status,
            StartedAt = startedAt,
            Combatants = combatants,
            EntryIds = combatants.Select(c => c.EntryId!.Value).Distinct().ToArray(),
        };
        var now = DateTimeOffset.UtcNow;
        var combats = new[]
        {
            Fight(CombatStatus.Active, now, Of(brynn), Of(goblin), Of(goblin)),
            Fight(CombatStatus.Finished, now, Of(brynn), Of(wolf, hidden: true)),
            Fight(CombatStatus.Draft, null, Of(goblin), Of(wolf)),
            // A Draft finished without starting was discarded: it never happened.
            Fight(CombatStatus.Finished, null, Of(goblin), Of(wolf)),
        };

        var player = ConnectionIndex.Pair(entries, [], [], combats, Player).Pairs;
        player.Keys.Should().Equal(new EntryPair(brynn.Id, goblin.Id));
        player.Values.Single().Combats.Should().Be(1);
        player.Values.Single().LastAt.Should().Be(now);

        ConnectionIndex.Pair(entries, [], [], combats, Dm).Pairs.Keys
            .Should().BeEquivalentTo([new EntryPair(brynn.Id, goblin.Id), new EntryPair(brynn.Id, wolf.Id)]);
    }

    [Fact]
    public void Notes_WithFewerThanTwoIds_AreNotSources()
    {
        var notes = new[] { new NoteMentions(Guid.NewGuid(), Guid.NewGuid(), [A], DateTimeOffset.UnixEpoch) };
        var entries = new[] { new Entry { Id = A, Name = "A" } };
        ConnectionIndex.Pair(entries, notes, [], [], Player).Pairs.Should().BeEmpty();
    }

    [Fact]
    public void TheGraph_OverTheCap_KeepsTheFocusThenTheHeaviest()
    {
        var hub = Entry("Hub");
        var spokes = Enumerable.Range(0, 5).Select(i => Entry($"Spoke {i}")).ToArray();
        var entries = spokes.Append(hub).ToDictionary(e => e.Id);
        // Spoke i is connected to the hub i + 1 times; the hub is the focus.
        var sources = spokes.SelectMany((s, i) => Enumerable.Range(0, i + 1).Select(_ => Note(hub.Id, s.Id)));
        var connections = ConnectionPairs.From(sources, new Dictionary<Guid, Guid>(), entries.Keys.ToHashSet()).Values;

        var graph = ConnectionGraph.Build(connections, entries, hub.Id, 1, Enum.GetValues<EntryKind>().ToHashSet(), maxNodes: 3);

        graph.Truncated.Should().BeTrue();
        graph.Nodes.Select(n => entries[n.EntryId].Name).Should().Equal("Hub", "Spoke 4", "Spoke 3");
        graph.Edges.Should().HaveCount(2);

        ConnectionGraph.Build(connections, entries, hub.Id, 1, Enum.GetValues<EntryKind>().ToHashSet(), maxNodes: 300)
            .Truncated.Should().BeFalse();
    }
}
