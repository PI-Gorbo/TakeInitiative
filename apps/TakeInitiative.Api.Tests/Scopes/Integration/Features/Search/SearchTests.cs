using FluentAssertions;
using Microsoft.Extensions.Logging;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Tests.Integration.Features.Combats;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Search;

/// <summary>
/// What a search answers, for one viewer who can see everything: the Entries ranking ladder, alias
/// and accent folding, the scope and section rules, sessions by number and title, the validation
/// 400s, the tsquery syntax that is searched as plain text, and <c>take</c> with <c>hasMore</c>.
/// <para>
/// Visibility has its own suites (<see cref="SearchLeakTests"/>,
/// <see cref="SearchVisibilityParityTests"/>), and <c>SearchSmokeTests</c> covers one case per
/// section; this extends it rather than repeating it.
/// </para>
/// </summary>
public class SearchTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private async Task<TestCampaign> NewCampaign(string name)
        => await TestCampaign.Create(fixture, name, withSecondPlayer: false);

    private async Task<SearchResponse> Search(Guid campaignId, string q, string? sections = null, int? take = null)
    {
        var response = await fixture.GetSearch(campaignId, q, sections, take);
        response.Should().Succeed();
        return response.Value;
    }

    private async Task<Guid> Entry(Guid campaignId, string name, EntryKind kind = EntryKind.Character, params string[] aliases)
    {
        var entry = await fixture.PostEntry(campaignId, name, kind);
        entry.Should().Succeed();
        if (aliases.Length > 0)
        {
            (await fixture.PutEntryAliases(campaignId, entry.Value.Id, aliases)).Should().Succeed();
        }
        return entry.Value.Id;
    }

    private async Task<SearchEntryHit[]> EntryHits(Guid campaignId, string q, int? take = null)
        => [.. (await Search(campaignId, q, sections: "entries", take: take))
            .Section(SearchSectionKey.Entries).Select(h => h.Entry!)];

    private static string Highlighted(Snippet snippet)
        => string.Join("|", snippet.Highlights.Select(h => snippet.Text.Substring(h.Start, h.Length)));

    // Entries: the ranking ladder (17a.8).

    [Fact]
    public async Task Entries_ComeBackExactThenPrefixThenWordPrefixThenSubstringThenFuzzy()
    {
        var campaign = await NewCampaign("Search: ladder");
        fixture.LoginAsUser(Users.Player);

        // One entry per rung of the ladder, for the query "gundren": equal, starts with, starts a
        // word, appears inside, and close enough by trigram (word_similarity 0.75, no substring).
        var exact = await Entry(campaign.Id, "Gundren");
        var prefix = await Entry(campaign.Id, "Gundren Rockseeker");
        var wordPrefix = await Entry(campaign.Id, "The Lost Gundren Mine", EntryKind.Place);
        var substring = await Entry(campaign.Id, "Regundren Keep", EntryKind.Place);
        var fuzzy = await Entry(campaign.Id, "Gundrem");

        var hits = await EntryHits(campaign.Id, "gundren", take: 5);
        hits.Select(h => h.Entry.Id).Should().Equal(exact, prefix, wordPrefix, substring, fuzzy);
        hits.Should().AllSatisfy(hit => hit.MatchedOn.Should().Be(SearchMatchedOn.Name));
        (await Search(campaign.Id, "gundren", sections: "entries", take: 5))
            .Sections.Single().HasMore.Should().BeFalse("five rungs, five hits");
    }

    [Theory]
    [InlineData("gundrn", "Gundren Rockseeker")]
    [InlineData("rockseker", "Gundren Rockseeker")]
    [InlineData("phandlin", "Phandalin")]
    public async Task ATypo_StillFindsTheName(string query, string expected)
    {
        var campaign = await NewCampaign($"Search: typo {query}");
        fixture.LoginAsUser(Users.Player);
        await Entry(campaign.Id, "Gundren Rockseeker");
        await Entry(campaign.Id, "Phandalin", EntryKind.Place);
        await Entry(campaign.Id, "Glasstaff");

        (await EntryHits(campaign.Id, query)).Should().ContainSingle()
            .Which.Entry.Name.Should().Be(expected);
    }

    [Fact]
    public async Task AWordThatIsMerelyDifferent_DoesNotMatch()
    {
        var campaign = await NewCampaign("Search: no false match");
        fixture.LoginAsUser(Users.Player);
        await Entry(campaign.Id, "Glasstaff");

        // word_similarity('goblin', 'glasstaff') is 0.125, far under the 0.5 threshold. Typo
        // tolerance that matched this would make every search a list of the whole wiki.
        (await EntryHits(campaign.Id, "goblin")).Should().BeEmpty();
    }

    [Fact]
    public async Task AnAliasHit_NamesTheAliasThatMatched()
    {
        var campaign = await NewCampaign("Search: aliases");
        fixture.LoginAsUser(Users.Player);
        var entryId = await Entry(campaign.Id, "Iarno Albrek", EntryKind.Character, "Glasstaff", "The Wizard of Tresendar");

        var glasstaff = (await EntryHits(campaign.Id, "glasstaff")).Should().ContainSingle().Subject;
        glasstaff.Entry.Id.Should().Be(entryId);
        glasstaff.MatchedOn.Should().Be(SearchMatchedOn.Alias);
        glasstaff.Alias.Should().Be("Glasstaff");
        glasstaff.Snippet.Should().BeNull("an alias hit has no snippet, it shows \"aka …\"");

        // The other alias, and a word inside it: the one that matched is the one reported.
        (await EntryHits(campaign.Id, "wizard")).Should().ContainSingle()
            .Which.Alias.Should().Be("The Wizard of Tresendar");
        // The name itself still wins when it matches, and then there is no alias.
        var byName = (await EntryHits(campaign.Id, "iarno")).Should().ContainSingle().Subject;
        byName.MatchedOn.Should().Be(SearchMatchedOn.Name);
        byName.Alias.Should().BeNull();
    }

    [Fact]
    public async Task AnAccentedNameOrAlias_IsFoundWithoutTheAccent()
    {
        var campaign = await NewCampaign("Search: accents");
        fixture.LoginAsUser(Users.Player);
        var entryId = await Entry(campaign.Id, "Gündren Rockseeker", EntryKind.Character, "Rocksëeker");

        (await EntryHits(campaign.Id, "gundren")).Should().ContainSingle()
            .Which.Entry.Id.Should().Be(entryId);
        // The accented alias folds too, and the better rung wins: "rockseeker" is exactly the
        // alias (0) where it only starts a word of the name (2), so the hit is the alias, reported
        // as it is stored.
        var alias = (await EntryHits(campaign.Id, "rockseeker")).Should().ContainSingle().Subject;
        alias.Entry.Id.Should().Be(entryId);
        alias.MatchedOn.Should().Be(SearchMatchedOn.Alias);
        alias.Alias.Should().Be("Rocksëeker");

        // An alias-only query folds as well: "wester" finds "Wëster".
        var other = await Entry(campaign.Id, "Sildar Hallwinter", EntryKind.Character, "Wëster");
        (await EntryHits(campaign.Id, "wester")).Should().ContainSingle()
            .Which.Entry.Id.Should().Be(other);
    }

    // Scope and splitting.

    [Fact]
    public async Task TheSectionsParameter_KeepsTheNamedSectionsOnly_AndIsCaseInsensitive()
    {
        var campaign = await NewCampaign("Search: sections");
        fixture.LoginAsUser(Users.Player);
        await Entry(campaign.Id, "Phandalin", EntryKind.Place);
        (await fixture.PostSessionNote(campaign.Id, "We reached Phandalin at dusk")).Should().Succeed();

        (await Search(campaign.Id, "phandalin")).Sections.Select(s => s.Key)
            .Should().Equal(SearchSectionKey.Entries, SearchSectionKey.Notes);
        (await Search(campaign.Id, "phandalin", sections: "notes")).Sections.Select(s => s.Key)
            .Should().Equal(SearchSectionKey.Notes);
        (await Search(campaign.Id, "phandalin", sections: "NOTES,Entries")).Sections.Select(s => s.Key)
            // The order is the server's, not the request's.
            .Should().Equal(SearchSectionKey.Entries, SearchSectionKey.Notes);
        (await Search(campaign.Id, "@phandalin", sections: "notes")).Sections.Select(s => s.Key)
            // The @ prefix is the narrower instruction, whatever sections were asked for.
            .Should().Equal(SearchSectionKey.Entries);
    }

    [Fact]
    public async Task ASingleCharacter_SearchesEntryNamesByPrefixAndSessionNumbersOnly()
    {
        var campaign = await NewCampaign("Search: one character");
        fixture.LoginAsUser(Users.Player);
        var glasstaff = await Entry(campaign.Id, "Glasstaff");
        await Entry(campaign.Id, "Regundren Keep", EntryKind.Place);
        (await fixture.PostSessionNote(campaign.Id, "The goblins fled")).Should().Succeed();

        var response = await Search(campaign.Id, "g");
        response.ShouldHaveSection(SearchSectionKey.Entries, "one character searches names by prefix")
            .Select(h => h.Entry!.Entry.Id)
            // Only a name that starts with it, never one that merely contains it.
            .Should().Equal(glasstaff);
        response.ShouldHaveNoSection(SearchSectionKey.Notes, "one character does not search note text");

        // The control for that absence: two characters do search note text, so the missing Notes
        // section above is the length rule at work and not a query that simply matched nothing.
        (await Search(campaign.Id, "go"))
            .ShouldHaveSection(SearchSectionKey.Notes, "two characters search note text")
            .Should().ContainSingle();

        // A digit searches session numbers.
        (await Search(campaign.Id, "1")).Section(SearchSectionKey.Sessions).Should().ContainSingle();
    }

    // Sessions.

    [Theory]
    [InlineData("12")]
    [InlineData("s12")]
    [InlineData("S 12")]
    [InlineData("session 12")]
    public async Task ASession_IsFoundByItsNumberHoweverItIsTyped(string query)
    {
        var campaign = await NewCampaign($"Search: session {query}");
        fixture.LoginAsUser(Users.DM);
        Guid twelfth = default;
        for (var number = 2; number <= 12; number++)
        {
            var started = await fixture.PostStartSession(campaign.Id, number);
            started.Should().Succeed();
            twelfth = started.Value.Id;
        }

        var hits = (await Search(campaign.Id, query)).Section(SearchSectionKey.Sessions);
        hits.Should().ContainSingle().Which.Session!.Session.Id.Should().Be(twelfth);
        hits.Single().Session!.Snippet.Should().BeNull("a number match does not echo the title back");
    }

    [Fact]
    public async Task ASession_IsFoundByAWordOfItsTitle_AndByNumberFirst()
    {
        var campaign = await NewCampaign("Search: session titles");
        fixture.LoginAsUser(Users.DM);
        var second = await fixture.PostStartSession(campaign.Id, 2);
        second.Should().Succeed();
        (await fixture.PutSessionTitle(campaign.Id, second.Value.Id, "The Triboar Trail")).Should().Succeed();
        var third = await fixture.PostStartSession(campaign.Id, 3);
        third.Should().Succeed();
        (await fixture.PutSessionTitle(campaign.Id, third.Value.Id, "Trail of 2 Wagons")).Should().Succeed();

        var byWord = (await Search(campaign.Id, "trail")).Section(SearchSectionKey.Sessions);
        byWord.Should().HaveCount(2);
        Highlighted(byWord[0].Session!.Snippet!).Should().Be("Trail");

        // "2" is a number and a word of the other title: the number match comes first.
        var byNumber = (await Search(campaign.Id, "2")).Section(SearchSectionKey.Sessions);
        byNumber[0].Session!.Session.Number.Should().Be(2, "a number match ranks before a title match");
        byNumber[0].Session!.Snippet.Should().BeNull();
    }

    [Fact]
    public async Task TwoFuzzyTitles_ComeBackByHowCloseTheyAre_NotBySessionNumber()
    {
        var campaign = await NewCampaign("Search: session similarity");
        fixture.LoginAsUser(Users.DM);
        var second = await fixture.PostStartSession(campaign.Id, 2);
        second.Should().Succeed();
        (await fixture.PutSessionTitle(campaign.Id, second.Value.Id, "Rockseeker Mine")).Should().Succeed();
        var third = await fixture.PostStartSession(campaign.Id, 3);
        third.Should().Succeed();
        (await fixture.PutSessionTitle(campaign.Id, third.Value.Id, "The Rocks of Neverwinter")).Should().Succeed();

        // Both titles are on the same rung of the ladder — fuzzy, neither holds "rockseker" — so
        // the tie-break decides: word_similarity is 0.75 for the mine and 0.5 for the rocks. Session
        // number is the last word, not the first, or the newer session would win on nothing.
        (await Search(campaign.Id, "rockseker")).Section(SearchSectionKey.Sessions)
            .Select(h => h.Session!.Session.Number)
            .Should().Equal(new[] { 2, 3 }, "the closer title comes first, though it is the older session");
    }

    [Fact]
    public async Task AFuzzyTitleHit_IsStillReturned_WithASnippetThatHighlightsNothing()
    {
        var campaign = await NewCampaign("Search: unhighlighted snippet");
        fixture.LoginAsUser(Users.DM);
        var second = await fixture.PostStartSession(campaign.Id, 2);
        second.Should().Succeed();
        (await fixture.PutSessionTitle(campaign.Id, second.Value.Id, "Rockseeker Mine")).Should().Succeed();

        // The title matched by trigram, so the tsquery ("rockseker":*) has nothing to mark in it.
        // An empty highlight list is not a reason to drop the hit: the words are what matched, and a
        // dropped row would shorten an answer Postgres has already counted for hasMore (17a.6).
        var hit = (await Search(campaign.Id, "rockseker")).Section(SearchSectionKey.Sessions)
            .Should().ContainSingle().Subject.Session!;
        hit.Session.Number.Should().Be(2);
        hit.Snippet.Should().NotBeNull();
        hit.Snippet!.Text.Should().Be("Rockseeker Mine");
        hit.Snippet!.Highlights.Should().BeEmpty();
    }

    // Snippets over Postgres: what PlainText takes out, and where that leaves the offsets.

    [Fact]
    public async Task ASnippetOfEscapedText_DropsTheEscapesAndTheMarkers_AndKeepsTheOffsetsRight()
    {
        var campaign = await NewCampaign("Search: escaped text");
        fixture.LoginAsUser(Users.Player);
        // Backslash escapes are removed by SearchSql.PlainText (SQL) and the markdown markers they
        // were escaping by Snippet.From (C#), so the offsets have to survive both. SnippetTests can
        // only cover the second half, because it never sees a backslash.
        (await fixture.PostSessionNote(campaign.Id, @"The \*Redbrands\* held the ridge above Phandalin"))
            .Should().Succeed();

        var snippet = (await Search(campaign.Id, "redbrands")).Section(SearchSectionKey.Notes)
            .Should().ContainSingle().Subject.Note!.Snippet;
        snippet.Text.Should().Be("The Redbrands held the ridge above Phandalin");
        snippet.Text.Should().NotContain("\\").And.NotContain("*");
        Highlighted(snippet).Should().Be("Redbrands");
        snippet.Highlights.Should().ContainSingle()
            .Which.Start.Should().Be(snippet.Text.IndexOf("Redbrands", StringComparison.Ordinal),
                "the offsets are over the text the reader sees, after both removals");
    }

    // Combats (18f): the section, after Sessions, and its order.

    private async Task<Guid> StartedCombat(Guid campaignId, string name, params string[] combatants)
    {
        var combat = await fixture.CreateCombat(campaignId, name);
        if (combatants.Length > 0)
        {
            var added = await fixture.AddAsDm(campaignId, combat.Id, [.. combatants.Select(c => (object)new { name = c })]);
            await fixture.Start(combat.Id, added.Combatants.ToDictionary(c => c.Id, _ => 10));
        }
        else
        {
            await fixture.Start(combat.Id, new Dictionary<Guid, int>());
        }
        return combat.Id;
    }

    [Fact]
    public async Task Combats_ComeBackByTheLadder_ThenLiveBeforeDraftsBeforeFinished_ThenNewest()
    {
        CombatTestKit.UseRealDice(fixture);
        var campaign = await NewCampaign("Search: combats");

        // The same rung (a word prefix of "ambush") in each status, and two finished ones to show
        // that newer comes first inside a status.
        var olderFinished = await StartedCombat(campaign.Id, "Old Ambush");
        var newerFinished = await StartedCombat(campaign.Id, "River Ambush");
        fixture.LoginAsUser(Users.DM);
        await fixture.Finish(campaign.Id, olderFinished).Ok();
        await fixture.Finish(campaign.Id, newerFinished).Ok();
        var draft = (await fixture.CreateCombat(campaign.Id, "Goblin Ambush")).Id;
        var live = await StartedCombat(campaign.Id, "Bridge Ambush");
        // A better rung beats every status: exact, then a prefix.
        var prefix = await StartedCombat(campaign.Id, "Ambush at the ford");
        fixture.LoginAsUser(Users.DM);
        await fixture.Finish(campaign.Id, prefix).Ok();
        var exact = (await fixture.CreateCombat(campaign.Id, "Ambush")).Id;

        fixture.LoginAsUser(Users.DM);
        var hits = (await Search(campaign.Id, "ambush", sections: "combats", take: 10)).Section(SearchSectionKey.Combats);
        hits.Select(h => h.Combat!.Combat.Id).Should().Equal(exact, prefix, live, draft, newerFinished, olderFinished);
        hits.Should().AllSatisfy(h =>
        {
            h.Kind.Should().Be(SearchHitKind.Combat);
            h.Combat!.MatchedCombatant.Should().BeNull();
            h.Combat!.SessionNumber.Should().Be(1);
        });
    }

    [Fact]
    public async Task ACombat_IsFoundByACombatantsName_AfterTheOtherSections()
    {
        CombatTestKit.UseRealDice(fixture);
        var campaign = await NewCampaign("Search: combat by combatant");
        var combatId = await StartedCombat(campaign.Id, "Triboar Trail", "Goblin 1", "Goblin 2", "Klarg");
        fixture.LoginAsUser(Users.Player);
        (await fixture.PostSessionNote(campaign.Id, "Klarg roared from the cave")).Should().Succeed();

        var response = await Search(campaign.Id, "klarg");
        response.Sections.Select(s => s.Key).Should().Equal(SearchSectionKey.Notes, SearchSectionKey.Combats);
        var hit = response.Section(SearchSectionKey.Combats).Should().ContainSingle().Subject.Combat!;
        hit.Combat.Id.Should().Be(combatId);
        hit.MatchedCombatant.Should().Be("Klarg");
        hit.Combat.Combatants.Select(c => c.Name).Should().BeEquivalentTo("Goblin 1", "Goblin 2", "Klarg");

        // Several combatants match: the first listed is named, once.
        (await Search(campaign.Id, "goblin", sections: "combats")).Section(SearchSectionKey.Combats)
            .Should().ContainSingle().Which.Combat!.MatchedCombatant.Should().Be("Goblin 1");
        // A single character searches no combats (the length rule).
        (await Search(campaign.Id, "k")).ShouldHaveNoSection(SearchSectionKey.Combats, "one character matches entry names and session numbers only");
        // And @ is entries only.
        (await Search(campaign.Id, "@klarg")).ShouldHaveNoSection(SearchSectionKey.Combats, "@ searches entries only");
    }

    // Nothing goes wrong quietly.

    [Fact]
    public async Task SearchesInNormalOperation_LogNoError()
    {
        var campaign = await NewCampaign("Search: no errors");
        fixture.LoginAsUser(Users.DM);
        var second = await fixture.PostStartSession(campaign.Id, 2);
        second.Should().Succeed();
        (await fixture.PutSessionTitle(campaign.Id, second.Value.Id, "The Triboar Trail")).Should().Succeed();
        (await fixture.PostSessionNote(campaign.Id, "Gundren spoke of Phandalin", Visibility.DM)).Should().Succeed();

        fixture.LoginAsUser(Users.Player);
        var entryId = await Entry(campaign.Id, "Gundren Rockseeker", EntryKind.Character, "Rocky");
        var entry = await fixture.GetEntry(campaign.Id, entryId);
        (await fixture.PutEntryArticle(campaign.Id, entryId, entry.Value.Article.Etag,
            [new BlockEdit(null, "A dwarf who hired the party in Neverwinter")])).Should().Succeed();
        (await fixture.PostSessionNote(campaign.Id, "We reached Phandalin at dusk")).Should().Succeed();

        foreach (var who in new[] { Users.DM, Users.Player })
        {
            foreach (var q in new[] { "gundren", "rocky", "neverwinter", "phandalin", "triboar", "s2", "@gundren", "g" })
            {
                fixture.LoginAsUser(who);
                (await fixture.GetSearch(campaign.Id, q)).Should().Succeed();
            }
        }

        // The log is actually being captured, or the assertion below would pass on an empty list
        // whatever the search did.
        fixture.Logs.Should().NotBeEmpty("the fixture captures what the API logs");

        // The belt-and-braces re-check (17a.6) logs an error and fails the request when a SQL
        // visibility fragment and the C# rule it mirrors have drifted. Nothing normal reaches it, and
        // a response cannot show that: a search that logged nothing looks exactly like one that did.
        fixture.Logs
            .Where(record => record.Level >= LogLevel.Error
                && record.Category.StartsWith("TakeInitiative.Api.Features.Search", StringComparison.Ordinal))
            .Should().BeEmpty("no search in normal operation may log an error");
    }

    // Validation, and the strings that only look like operators.

    [Fact]
    public async Task AnOverlongQuery_IsA400()
    {
        var campaign = await NewCampaign("Search: overlong");
        fixture.LoginAsUser(Users.Player);
        var tooLong = new string('a', SearchQuery.MaxLength + 1);

        await fixture.ExpectStatus(HttpMethod.Get, SearchUrl(campaign.Id, tooLong), null, 400);
        // Exactly the limit is fine, and the prefix is not counted.
        (await Search(campaign.Id, new string('a', SearchQuery.MaxLength))).Query.Should().HaveLength(SearchQuery.MaxLength);
        (await Search(campaign.Id, "@" + new string('a', SearchQuery.MaxLength))).Query.Should().HaveLength(SearchQuery.MaxLength);
    }

    [Fact]
    public async Task TsQueryOperators_AreSearchedAsPlainText()
    {
        var campaign = await NewCampaign("Search: operators");
        fixture.LoginAsUser(Users.Player);
        var note = await fixture.PostSessionNote(campaign.Id, "The plan is a & b | !c tonight");
        note.Should().Succeed();

        // Every word must match, and the punctuation is not syntax: it is dropped with the rest of
        // what is not a letter or a digit.
        var hits = (await Search(campaign.Id, "a & b | !c")).Section(SearchSectionKey.Notes);
        hits.Should().ContainSingle().Which.Note!.Id.Should().Be(note.Value.Id);

        // A word the note does not have makes the whole query miss: there is no OR.
        (await Search(campaign.Id, "a & b | !z")).Section(SearchSectionKey.Notes).Should().BeEmpty();
    }

    [Theory]
    [InlineData("'")]
    [InlineData(":*")]
    [InlineData("\\")]
    [InlineData("&|!")]
    public async Task AQueryOfPunctuationAlone_IsA200WithNothingFound(string query)
    {
        var campaign = await NewCampaign($"Search: punctuation {query}");
        fixture.LoginAsUser(Users.Player);
        (await fixture.PostSessionNote(campaign.Id, "A note with several words in it")).Should().Succeed();
        await Entry(campaign.Id, "Glasstaff");

        var response = await Search(campaign.Id, query);
        response.Query.Should().Be(query);
        response.Sections.Should().BeEmpty("there is no word to match, and no operator to run");
    }

    // take and hasMore.

    [Fact]
    public async Task TakeCutsEachSection_AndHasMoreSaysWhetherAnythingWasCut()
    {
        var campaign = await NewCampaign("Search: take");
        fixture.LoginAsUser(Users.Player);
        for (var i = 0; i < 3; i++)
        {
            (await fixture.PostSessionNote(campaign.Id, $"Torchlight in the tunnel, {i} paces on")).Should().Succeed();
        }
        await Entry(campaign.Id, "Torchlight Hall", EntryKind.Place);
        await Entry(campaign.Id, "Torchlight Bearer");

        var one = (await Search(campaign.Id, "torchlight", take: 1)).Sections;
        one.Should().HaveCount(2);
        one.Should().AllSatisfy(section =>
        {
            section.Hits.Should().ContainSingle();
            section.HasMore.Should().BeTrue();
        });

        var three = await Search(campaign.Id, "torchlight", take: 3);
        var notes = three.Sections.Single(s => s.Key == SearchSectionKey.Notes);
        notes.Hits.Should().HaveCount(3);
        notes.HasMore.Should().BeFalse("three notes match and three were asked for");
        var entries = three.Sections.Single(s => s.Key == SearchSectionKey.Entries);
        entries.Hits.Should().HaveCount(2);
        entries.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task TakeAtEitherEndOfItsRange_IsAccepted()
    {
        var campaign = await NewCampaign("Search: take bounds");
        fixture.LoginAsUser(Users.Player);
        for (var i = 0; i < 3; i++)
        {
            (await fixture.PostSessionNote(campaign.Id, $"Torchlight in the tunnel, {i} paces on")).Should().Succeed();
        }

        // 0 and 21 are 400s (SearchSmokeTests); 1 and 20 are the ends of the range and are answered.
        var one = (await Search(campaign.Id, "torchlight", take: 1)).Sections
            .Single(s => s.Key == SearchSectionKey.Notes);
        one.Hits.Should().ContainSingle();
        one.HasMore.Should().BeTrue();

        var twenty = (await Search(campaign.Id, "torchlight", take: GetSearch.MaxTake)).Sections
            .Single(s => s.Key == SearchSectionKey.Notes);
        twenty.Hits.Should().HaveCount(3);
        twenty.HasMore.Should().BeFalse();
    }
}
