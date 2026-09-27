using System.Diagnostics;
using System.Text;
using Alba;
using FluentAssertions;
using Xunit.Abstractions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Search;

/// <summary>
/// The performance budgets of 17a step 12, measured at the API over the large seed
/// (<see cref="SearchSeed"/>): 200 mixed queries, with p50 and p95 reported.
/// <para>
/// It runs <b>only</b> when <c>TI_PERF=1</c> and returns early otherwise, so CI times nothing: a
/// number measured on a shared runner under load would either be meaningless or fail the build for
/// no reason. Run it by hand with
/// <c>TI_PERF=1 dotnet test --filter SearchPerfTests</c>.
/// </para>
/// <para>
/// The seed is built once and shared by every test here: 5,000 notes and 1,000 entries through
/// real event appends is not something to do three times.
/// </para>
/// <para>
/// <b>As measured</b> (Apple Silicon, Postgres 15 in Docker, 2026-09), with the article vector
/// stored (17a.3): all sections at <c>take=5</c> <b>p50 17.6 ms / p95 62.4 ms</b>, one character
/// p50 10.8 ms / p95 13.6 ms, largest response at <c>take=5</c> 11.4 KB. Every budget holds.
/// Before the stored column, on the same machine and seed, all sections came to p50 51.9 ms /
/// p95 101.6 ms — over both — and one character to p50 20.2 ms / p95 24.2 ms.
/// </para>
/// <para>
/// <b>Where the milliseconds went.</b> The article query was 33 ms of the old p50, and 30 ms of that
/// was the prefilter: the planner costs a sequential scan below a GIN partial-match scan and never
/// used an expression index for a prefix <c>tsquery</c> at this size, so Postgres computed
/// <c>to_tsvector(jsonb_path_query_array(…))</c> for every entry in the table. Stored in a column it
/// is 0.3 ms, whichever plan is chosen. The entry matcher's scan of 1,000 entry documents came down
/// with it, 4.9 ms to 2.0 ms, because <c>EntryRowFitsInline</c> keeps an entry's row in its page:
/// <c>data</c> is no longer compressed, so a name match no longer decompresses every row it reads.
/// What is left is the framework and the round trip (1.5 ms), the entry matcher's
/// <c>lower(unaccent(…))</c> and <c>word_similarity</c> over 2,739 names and aliases, the Notes,
/// Images and Sessions queries, the entry loads and the mention counts.
/// </para>
/// <para>
/// The cost is still <b>linear in the number of entries</b> — names and aliases are scanned per
/// campaign, by design (17a.3) — so the budget holds with room at 1,000 entries and would be
/// reached again somewhere well above it. Keeping the row inline costs disk: <c>mt_doc_entry</c> is
/// 4.0 MB for 1,000 entries where it was 1.6 MB compressed.
/// </para>
/// </summary>
public class SearchPerfTests(AuthenticatedWebAppWithDatabaseFixture fixture, ITestOutputHelper output)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    /// <summary>Whether to measure at all. Anything but <c>1</c> means "return early".</summary>
    private static bool Enabled => Environment.GetEnvironmentVariable("TI_PERF") == "1";

    public const int Queries = 200;
    public const int WarmUp = 20;

    public const double AllSectionsP50Ms = 40;
    public const double AllSectionsP95Ms = 100;
    public const double OneCharacterP95Ms = 60;
    public const int MaxResponseBytes = 15 * 1024;

    private static readonly SemaphoreSlim SeedGate = new(1, 1);
    private static SeededSearchCampaign? _seeded;

    private async Task<SeededSearchCampaign> Seeded()
    {
        await SeedGate.WaitAsync();
        try
        {
            if (_seeded is null)
            {
                var started = Stopwatch.StartNew();
                _seeded = await SearchSeed.Create(fixture);
                output.WriteLine($"Seeded {SearchSeed.NoteCount} notes, {SearchSeed.EntryCount} entries "
                    + $"and {SearchSeed.SessionCount} sessions in {started.Elapsed.TotalSeconds:F1} s.");
            }
            return _seeded;
        }
        finally
        {
            SeedGate.Release();
        }
    }

    /// <summary>One search, timed from before the request to after the body is read.</summary>
    private async Task<(double Milliseconds, int Bytes)> Timed(Guid campaignId, Users who, string q, int take = 5)
    {
        fixture.LoginAsUser(who);
        var url = SearchUrl(campaignId, q, take: take);
        var watch = Stopwatch.StartNew();
        var result = await fixture.AlbaHost.Scenario(_ =>
        {
            _.Get.Url(url);
            _.StatusCodeShouldBe(200);
        });
        var body = await result.ReadAsTextAsync();
        watch.Stop();
        return (watch.Elapsed.TotalMilliseconds, Encoding.UTF8.GetByteCount(body));
    }

    private static double Percentile(List<double> sorted, double fraction)
        => sorted[Math.Clamp((int)Math.Ceiling(fraction * sorted.Count) - 1, 0, sorted.Count - 1)];

    private void Report(string what, List<double> times, int maxBytes)
    {
        times.Sort();
        output.WriteLine(
            $"{what}: n={times.Count} p50={Percentile(times, 0.50):F1} ms p95={Percentile(times, 0.95):F1} ms "
            + $"min={times[0]:F1} max={times[^1]:F1} largest response={maxBytes} bytes");
    }

    /// <summary>
    /// The mix, in the proportions a real ⌘K sees: mostly words of middling frequency and names
    /// being typed, with a few of the worst case (a word in a sixth of the notes), typos, two-word
    /// queries, session numbers and titles.
    /// </summary>
    private static List<string> Mix(SeededSearchCampaign seed)
    {
        var mix = new List<string>();
        for (var i = 0; mix.Count < Queries + WarmUp; i++)
        {
            mix.Add(seed.CommonWords[i % seed.CommonWords.Count]);
            mix.Add(seed.MidWords[i % seed.MidWords.Count]);
            mix.Add(seed.MidWords[(i + 5) % seed.MidWords.Count]);
            mix.Add(seed.RareWords[i % seed.RareWords.Count]);
            mix.Add("gundr");
            mix.Add("gundrn");
            mix.Add($"{seed.MidWords[i % seed.MidWords.Count]} {seed.CommonWords[(i + 3) % seed.CommonWords.Count]}");
            mix.Add($"s{i % SearchSeed.SessionCount + 1}");
            mix.Add("triboar");
            mix.Add("rocksee");
            mix.Add(seed.EntryNames[i % seed.EntryNames.Count].Split(' ')[0]);
            mix.Add("session 12");
        }
        return mix.Take(Queries + WarmUp).ToList();
    }

    [Fact]
    public async Task AllSections_AtTakeFive_AreInsideTheBudget()
    {
        if (!Enabled)
        {
            return;
        }

        var seed = await Seeded();
        var mix = Mix(seed);
        var viewers = new[] { Users.DM, Users.Outsider, Users.Player };

        // Warm up: the first request per code path pays for FastEndpoints' and Npgsql's one-off
        // work, which is not what the budget is about.
        for (var i = 0; i < WarmUp; i++)
        {
            await Timed(seed.CampaignId, viewers[i % viewers.Length], mix[i]);
        }

        var times = new List<double>(Queries);
        var largest = 0;
        for (var i = 0; i < Queries; i++)
        {
            var (ms, bytes) = await Timed(seed.CampaignId, viewers[i % viewers.Length], mix[WarmUp + i]);
            times.Add(ms);
            largest = Math.Max(largest, bytes);
        }

        Report("GET search, all sections, take=5", times, largest);
        Percentile(times, 0.50).Should().BeLessThanOrEqualTo(AllSectionsP50Ms);
        Percentile(times, 0.95).Should().BeLessThanOrEqualTo(AllSectionsP95Ms);
        largest.Should().BeLessThanOrEqualTo(MaxResponseBytes, "a response at take=5 is at most 15 KB");
    }

    [Fact]
    public async Task AOneCharacterQuery_IsInsideItsBudget()
    {
        if (!Enabled)
        {
            return;
        }

        var seed = await Seeded();
        // Entry names by prefix and session numbers only: the length rule is there so one
        // character cannot ask for most of the campaign.
        var singles = "abcdefghijklmnopqrstuvwxyz123456789".Select(c => c.ToString()).ToList();

        for (var i = 0; i < WarmUp; i++)
        {
            await Timed(seed.CampaignId, Users.DM, singles[i % singles.Count]);
        }

        var times = new List<double>();
        var largest = 0;
        for (var i = 0; i < Queries; i++)
        {
            var (ms, bytes) = await Timed(seed.CampaignId, i % 2 == 0 ? Users.DM : Users.Player, singles[i % singles.Count]);
            times.Add(ms);
            largest = Math.Max(largest, bytes);
        }

        Report("GET search, one character", times, largest);
        Percentile(times, 0.95).Should().BeLessThanOrEqualTo(OneCharacterP95Ms);
        largest.Should().BeLessThanOrEqualTo(MaxResponseBytes);
    }

    [Fact]
    public async Task AResponseAtTakeFive_IsSmall()
    {
        if (!Enabled)
        {
            return;
        }

        var seed = await Seeded();
        var sizes = new List<int>();
        foreach (var query in Mix(seed).Take(60))
        {
            foreach (var who in new[] { Users.DM, Users.Player })
            {
                sizes.Add((await Timed(seed.CampaignId, who, query)).Bytes);
            }
        }

        output.WriteLine($"Response size at take=5: n={sizes.Count} max={sizes.Max()} bytes, mean={sizes.Average():F0} bytes");
        sizes.Max().Should().BeLessThanOrEqualTo(MaxResponseBytes);
    }
}
