using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Search;

/// <summary>
/// What <see cref="SearchSeed"/> made, and the words the perf run queries with. The member ids
/// are per campaign; the three that belong to a signed-in user are named, because only those can
/// call the API.
/// </summary>
/// <param name="CampaignId">The seeded campaign.</param>
/// <param name="OwnerDmMemberId">The owning DM (<see cref="Users.DM"/>).</param>
/// <param name="SecondDmMemberId">The second DM (<see cref="Users.Outsider"/>, promoted).</param>
/// <param name="PlayerMemberId">The player (<see cref="Users.Player"/>).</param>
/// <param name="DmMemberIds">Every DM, including the two above and one that is nobody's account.</param>
/// <param name="PlayerMemberIds">Every player, including the one above.</param>
/// <param name="EntryNames">Every entry's name, in creation order.</param>
/// <param name="CommonWords">Words in roughly a sixth of the notes: the worst case for the note query.</param>
/// <param name="MidWords">Words in a few per cent of the notes: what most queries meet.</param>
/// <param name="RareWords">Words planted in a handful of notes each.</param>
public record SeededSearchCampaign(
    Guid CampaignId,
    Guid OwnerDmMemberId,
    Guid SecondDmMemberId,
    Guid PlayerMemberId,
    IReadOnlyList<Guid> DmMemberIds,
    IReadOnlyList<Guid> PlayerMemberIds,
    IReadOnlyList<Guid> SessionIds,
    IReadOnlyList<Guid> EntryIds,
    IReadOnlyList<string> EntryNames,
    IReadOnlyList<string> CommonWords,
    IReadOnlyList<string> MidWords,
    IReadOnlyList<string> RareWords);

/// <summary>
/// The large seed of 17a step 12: one campaign, 3 DMs and 5 players, 150 sessions, 5,000 notes of
/// about 300 characters (10% <c>DM</c>, 5% <c>Me</c>, 2% hidden, 15% with images) and 1,000
/// entries, each with 2 aliases and an article of 5 blocks, one in five of them secret.
/// <para>
/// Events are appended <b>straight to the streams</b>, so the real inline projections run and the
/// expression indexes are maintained by Postgres in the same transaction: the seed exercises what
/// search actually reads. Inserting documents would not.
/// </para>
/// <para>
/// Everything random comes from <see cref="Seed"/>, a fixed number, so a failure reproduces: the
/// same words land in the same notes on every run.
/// </para>
/// </summary>
public static class SearchSeed
{
    public const int Seed = 170_412;

    public const int SessionCount = 150;
    public const int NoteCount = 5_000;
    public const int EntryCount = 1_000;
    public const int BlocksPerEntry = 5;

    /// <summary>How many streams go into one transaction. Big enough to be quick, small enough to keep the append batch sane.</summary>
    private const int BatchSize = 250;

    /// <summary>A needle every perf query can rely on, in one entry's name and one note's text.</summary>
    public const string KnownEntryName = "Gundren Rockseeker";
    public const string KnownAlias = "Rockseeker";

    /// <summary>
    /// The words that come up all the time. A note draws <see cref="CommonShare"/> of its words from
    /// these, so each lands in roughly a sixth of the notes: the worst case a real query meets.
    /// </summary>
    private static readonly string[] Common =
    [
        "party", "road", "camp", "torch", "rope", "dagger", "goblin", "cave", "ambush", "wagon",
        "mine", "ruin", "shrine", "altar", "scroll", "potion", "gold", "silver", "banner", "gate",
        "bridge", "river", "forest", "hill", "keep", "tower", "cellar", "chest", "letter", "map",
        "trail", "watch", "night", "morning", "storm", "spider", "wolf", "bandit", "cultist", "ghoul",
        "spell", "shield", "armour", "bow", "arrow", "horse", "cart", "market", "tavern", "inn",
    ];

    /// <summary>
    /// How much of a note is drawn from <see cref="Common"/>. The rest comes from the long tail, so
    /// the corpus has a realistic vocabulary: a campaign's notes are not written out of fifty words,
    /// and a benchmark whose every query matched half the table would measure the wrong thing.
    /// </summary>
    private const double CommonShare = 0.2;

    /// <summary>The size of the long tail. Each of its words lands in a few per cent of the notes.</summary>
    private const int TailSize = 600;

    private static readonly string[] Onsets =
        ["b", "br", "d", "dr", "f", "g", "gr", "h", "k", "kr", "l", "m", "n", "p", "r", "s", "sh", "t", "th", "v"];
    private static readonly string[] Vowels = ["a", "e", "i", "o", "u", "ae", "ei", "ou"];
    private static readonly string[] Codas = ["l", "n", "r", "s", "th", "ld", "nd", "st", "rk", "m"];

    /// <summary>Words planted in a few notes each: the best case, and what a real query usually is.</summary>
    private static readonly string[] Rare =
    [
        "zanthor", "quelline", "mirabar", "thundertree", "cragmaw", "wyvern", "phandalin", "neverwinter",
    ];

    private static readonly string[] TitleWords =
    [
        "Triboar", "Trail", "Redbrand", "Hideout", "Wave", "Echo", "Cave", "Manor", "Tomb", "Spire",
    ];

    private static readonly string[] NameFirst =
    [
        "Gundren", "Sildar", "Iarno", "Klarg", "Yeemik", "Droop", "Toblen", "Elsa", "Halia", "Daran",
        "Sister", "Linene", "Harbin", "Mirna", "Nars", "Reidoth", "Venomfang", "Agatha", "Hamun", "Vyerith",
    ];

    private static readonly string[] NameLast =
    [
        "Rockseeker", "Hallwinter", "Albrek", "Stonehill", "Thornton", "Dendrar", "Wester", "Barthen",
        "Graywind", "Ironfist", "Silvermane", "Blackspear", "Oakhollow", "Duskwater", "Emberfall",
    ];

    /// <summary>
    /// Seeds the campaign and returns what it holds. It is called once per fixture: seeding takes
    /// thousands of appends, so the perf tests share one.
    /// </summary>
    public static async Task<SeededSearchCampaign> Create(AuthenticatedWebAppWithDatabaseFixture fixture)
    {
        // Membership goes through the API, so the three accounts that search are real members in
        // the real way. Session 1 comes with the campaign.
        var campaign = await TestCampaign.Create(fixture, "Search perf seed");
        await campaign.PromoteToDm(fixture, campaign.SecondPlayerMemberId!.Value);

        var store = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>();
        var random = new Random(Seed);

        var dms = new List<Guid> { campaign.DmMemberId, campaign.SecondPlayerMemberId.Value };
        var players = new List<Guid> { campaign.PlayerMemberId };
        await using (var session = store.LightweightSession())
        {
            // The rest of the table: one more DM and four more players. They never sign in, so
            // they need no account, only a member id on the campaign.
            var extra = Enumerable.Range(0, 5).Select(_ => (MemberId: Guid.NewGuid(), UserId: Guid.NewGuid())).ToList();
            foreach (var (memberId, userId) in extra)
            {
                session.Events.Append(campaign.Id, new MemberJoined(Actor.Member(campaign.DmMemberId), memberId, userId));
            }
            session.Events.Append(
                campaign.Id, new MemberRoleChanged(Actor.Member(campaign.DmMemberId), extra[0].MemberId, Role.DM));
            await session.SaveChangesAsync();

            dms.Add(extra[0].MemberId);
            players.AddRange(extra.Skip(1).Select(x => x.MemberId));
        }

        await using (var query = store.QuerySession())
        {
            var stored = await query.LoadAsync<Api.Features.Campaigns.Campaign>(campaign.Id);
            stored!.Members.Should().HaveCount(8, "3 DMs and 5 players");
            stored.Members.Count(m => m.Role == Role.DM).Should().Be(3);
        }

        var members = dms.Concat(players).ToList();
        var sessionIds = await Sessions(store, campaign.Id, random, members);
        var (entryIds, entryNames) = await Entries(store, campaign.Id, random, dms, members);
        await Notes(store, campaign.Id, random, sessionIds, dms, players, entryIds, entryNames);
        await Analyze(store);

        // The events really did project: a seed that quietly wrote nothing would make every number
        // below look wonderful.
        await using (var query = store.QuerySession())
        {
            (await query.Query<SessionNote>().CountAsync(n => n.CampaignId == campaign.Id)).Should().Be(NoteCount);
            (await query.Query<Entry>().CountAsync(e => e.CampaignId == campaign.Id)).Should().Be(EntryCount);
            (await query.Query<Session>().CountAsync(s => s.CampaignId == campaign.Id)).Should().Be(SessionCount);
        }

        return new SeededSearchCampaign(
            campaign.Id,
            campaign.DmMemberId,
            campaign.SecondPlayerMemberId.Value,
            campaign.PlayerMemberId,
            dms,
            players,
            sessionIds,
            entryIds,
            entryNames,
            CommonWords: [.. Common.Take(10)],
            MidWords: [.. TailWords.Take(10)],
            RareWords: Rare);
    }

    /// <summary>Sessions 2 to <see cref="SessionCount"/>, most of them titled. Session 1 came with the campaign.</summary>
    private static async Task<List<Guid>> Sessions(
        IDocumentStore store, Guid campaignId, Random random, List<Guid> members)
    {
        var ids = new List<Guid>();
        await using var query = store.QuerySession();
        var first = await query.Query<Session>().Where(s => s.CampaignId == campaignId).SingleAsync();
        ids.Add(first.Id);

        for (var start = 2; start <= SessionCount; start += BatchSize)
        {
            await using var session = store.LightweightSession();
            for (var number = start; number < Math.Min(start + BatchSize, SessionCount + 1); number++)
            {
                var id = Guid.NewGuid();
                var actor = Actor.Member(members[random.Next(members.Count)]);
                session.Events.StartStream<Session>(id, new SessionStarted(actor, campaignId, number));
                if (number % 4 != 0)
                {
                    var title = $"The {Pick(random, TitleWords)} {Pick(random, TitleWords)}";
                    session.Events.Append(id, new SessionTitleChanged(actor, title));
                }
                ids.Add(id);
            }
            await session.SaveChangesAsync();
        }
        return ids;
    }

    /// <summary>
    /// <see cref="EntryCount"/> entries: a name, two aliases and an article of
    /// <see cref="BlocksPerEntry"/> blocks, one of which is secret (<c>DM</c> or <c>Me</c>, owned
    /// by a DM). One entry in ten is not <c>Everyone</c>, so the visibility fragments do real work.
    /// </summary>
    private static async Task<(List<Guid> Ids, List<string> Names)> Entries(
        IDocumentStore store, Guid campaignId, Random random, List<Guid> dms, List<Guid> members)
    {
        var ids = new List<Guid>();
        var names = new List<string>();

        for (var start = 0; start < EntryCount; start += BatchSize)
        {
            await using var session = store.LightweightSession();
            for (var index = start; index < Math.Min(start + BatchSize, EntryCount); index++)
            {
                var id = Guid.NewGuid();
                // Unique, and the first one is the needle every query can rely on.
                var name = index == 0
                    ? KnownEntryName
                    : $"{Pick(random, NameFirst)} {Pick(random, NameLast)} {index}";
                var creator = members[random.Next(members.Count)];
                var actor = Actor.Member(creator);
                var visibility = (index % 10) switch
                {
                    0 when index > 0 => Visibility.DM,
                    5 => Visibility.Me,
                    _ => Visibility.Everyone,
                };

                session.Events.StartStream<Entry>(id, new EntryCreated(
                    actor, campaignId, creator, name, (EntryKind)(index % 6), visibility));
                session.Events.Append(id, new EntryAliasAdded(actor, index == 0 ? KnownAlias : $"{Pick(random, NameLast)} {index}"));
                session.Events.Append(id, new EntryAliasAdded(actor, $"{Pick(random, NameFirst)}{index}"));

                var blocks = Enumerable.Range(0, BlocksPerEntry).Select(b =>
                {
                    var secret = b == index % BlocksPerEntry;
                    return new ArticleBlock
                    {
                        Id = Guid.NewGuid(),
                        Text = Sentence(random, words: 18),
                        Visibility = secret ? (b % 2 == 0 ? Visibility.DM : Visibility.Me) : Visibility.Everyone,
                        OwnerMemberId = secret ? dms[random.Next(dms.Count)] : creator,
                    };
                }).ToList();
                session.Events.Append(id, new EntryArticleEdited(actor, blocks));

                ids.Add(id);
                names.Add(name);
            }
            await session.SaveChangesAsync();
        }

        return (ids, names);
    }

    /// <summary>
    /// <see cref="NoteCount"/> notes of about 300 characters, spread over the sessions and the
    /// members, with the step's mix of visibilities, hidden notes, images and mentions.
    /// </summary>
    private static async Task Notes(
        IDocumentStore store, Guid campaignId, Random random, List<Guid> sessionIds,
        List<Guid> dms, List<Guid> players, List<Guid> entryIds, List<string> entryNames)
    {
        var authors = dms.Concat(players).ToList();

        for (var start = 0; start < NoteCount; start += BatchSize)
        {
            await using var session = store.LightweightSession();
            for (var index = start; index < Math.Min(start + BatchSize, NoteCount); index++)
            {
                var id = Guid.NewGuid();
                var author = authors[random.Next(authors.Count)];
                var actor = Actor.Member(author);
                var visibility = (index % 20) switch
                {
                    0 or 10 => Visibility.DM,   // 10%
                    5 => Visibility.Me,         //  5%
                    _ => Visibility.Everyone,
                };
                var images = index % 20 < 3    // 15%
                    ? new[] { new NoteImage(Guid.NewGuid(), 1200, 800) }
                    : null;

                var text = Sentence(random, words: 45);
                // One note in eight mentions an entry, so mention counts and the "display text is
                // searched" rule are exercised at scale.
                if (index % 8 == 0)
                {
                    var which = random.Next(entryIds.Count);
                    text = $"@[{entryNames[which]}](entry:{entryIds[which]}) {text}";
                }
                // A rare word in a handful of notes each: what a real query looks like.
                if (index % 97 == 0)
                {
                    text = $"{Rare[index / 97 % Rare.Length]} {text}";
                }

                session.Events.StartStream<SessionNote>(id, new SessionNotePosted(
                    Actor: actor,
                    CampaignId: campaignId,
                    SessionId: sessionIds[random.Next(sessionIds.Count)],
                    AuthorMemberId: author,
                    Text: text,
                    Visibility: visibility,
                    IsRecap: index % 50 == 0,
                    AddedLater: false,
                    Images: images));

                if (index % 50 == 7)    // 2%
                {
                    session.Events.Append(id, new SessionNoteHidden(Actor.Member(dms[0])));
                }
            }
            await session.SaveChangesAsync();
        }
    }

    private static string Pick(Random random, string[] from) => from[random.Next(from.Length)];

    /// <summary>
    /// The long tail, built from syllables so the words look and measure like words (five to nine
    /// characters), and always the same words, because <see cref="Seed"/> is fixed.
    /// </summary>
    private static string[] Tail()
    {
        var random = new Random(Seed + 1);
        var words = new HashSet<string>();
        while (words.Count < TailSize)
        {
            var word = Pick(random, Onsets) + Pick(random, Vowels) + Pick(random, Codas)
                + (random.Next(2) == 0 ? Pick(random, Vowels) + Pick(random, Codas) : string.Empty);
            words.Add(word);
        }
        return [.. words];
    }

    private static readonly string[] TailWords = Tail();

    /// <summary>A sentence of <paramref name="words"/> words: 45 of these average about 300 characters.</summary>
    private static string Sentence(Random random, int words)
        => string.Join(' ', Enumerable.Range(0, words)
            .Select(_ => random.NextDouble() < CommonShare ? Pick(random, Common) : Pick(random, TailWords)));

    /// <summary>
    /// The statistics Postgres plans with. Autovacuum analyses a real table soon after it fills, but
    /// a table bulk-loaded a second ago has none, and the planner then reads the campaign index and
    /// evaluates <c>to_tsvector</c> on every row rather than using the search index — which measures
    /// a state no running database is ever in for long.
    /// </summary>
    private static async Task Analyze(IDocumentStore store)
    {
        await using var connection = (Npgsql.NpgsqlConnection)store.Storage.Database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            ANALYZE public.mt_doc_sessionnote;
            ANALYZE public.mt_doc_entry;
            ANALYZE public.mt_doc_session;
            """;
        await command.ExecuteNonQueryAsync();
    }
}
