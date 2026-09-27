using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Search;

/// <summary>
/// One test per visibility case of 17a step 14, and the core of the step. Each plants a unique
/// token in the unit under test and then searches as every relevant viewer — the author or
/// creator, another DM, a player, and a member of another campaign — asserting on the hits, the
/// snippets, <c>hasMore</c> and the order.
/// <para>
/// Every case gets its own campaign, so the only thing in it that could match the token is the
/// thing the case planted: an empty answer means the search found nothing, not that something
/// else outranked it. Each campaign has two DMs (the owner and <see cref="Users.Outsider"/>,
/// promoted) and two players (<see cref="Users.Player"/> and <see cref="Users.Player2"/>), so
/// "another DM" and "another player" are always someone. <see cref="Users.Stranger"/> joins
/// nothing.
/// </para>
/// <para>
/// These tests check what a viewer <b>receives</b>, which is the SQL fragment and the providers'
/// C# re-check together (17a.6, "Belt and braces"). The re-check drops a drifted row, so a
/// fragment that leaked would not show up here — it shows up in
/// <see cref="SearchVisibilityParityTests"/>, which is why that suite exists as well.
/// </para>
/// </summary>
public class SearchLeakTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    /// <summary>The planted word. It is in no name, note or block that a test did not put it in.</summary>
    private const string Token = "zanthor";

    private record World(TestCampaign Campaign)
    {
        public Guid Id => Campaign.Id;
    }

    /// <summary>A campaign of its own: DM (owner), Outsider (a second DM), Player and Player2.</summary>
    private async Task<World> NewWorld(string name)
    {
        var campaign = await TestCampaign.Create(fixture, name);
        await campaign.PromoteToDm(fixture, campaign.SecondPlayerMemberId!.Value);
        await campaign.Join(fixture, Users.Player2);
        return new World(campaign);
    }

    private async Task<SearchResponse> Search(World world, Users who, string q, string? sections = null, int? take = null)
    {
        fixture.LoginAsUser(who);
        var response = await fixture.GetSearch(world.Id, q, sections, take);
        response.Should().Succeed();
        return response.Value;
    }

    private static string Highlighted(Snippet snippet)
        => string.Join("|", snippet.Highlights.Select(h => snippet.Text.Substring(h.Start, h.Length)));

    /// <summary>The one note hit of a section, with the token highlighted in its snippet.</summary>
    private static SearchNoteHit OneNote(SearchResponse response, SearchSectionKey key, Guid noteId)
    {
        var section = response.Sections.Should().ContainSingle(s => s.Key == key).Subject;
        section.HasMore.Should().BeFalse("one hit is not more than take");
        var hit = section.Hits.Should().ContainSingle().Subject;
        hit.Kind.Should().Be(SearchHitKind.Note);
        hit.Note!.Id.Should().Be(noteId);
        Highlighted(hit.Note!.Snippet).Should().Be(Token);
        return hit.Note!;
    }

    private static SearchEntryHit OneEntry(SearchResponse response, Guid entryId)
    {
        var section = response.Sections.Should().ContainSingle(s => s.Key == SearchSectionKey.Entries).Subject;
        section.HasMore.Should().BeFalse();
        var hit = section.Hits.Should().ContainSingle().Subject;
        hit.Kind.Should().Be(SearchHitKind.Entry);
        hit.Entry!.Entry.Id.Should().Be(entryId);
        return hit.Entry!;
    }

    /// <summary>
    /// Nothing at all: no section, so no hit, no snippet, no <c>hasMore</c> and no place in an
    /// order. An empty section would still be an answer ("there is a section for this"), which is
    /// why the endpoint leaves empty ones out.
    /// </summary>
    private static void Nothing(SearchResponse response)
        => response.Sections.Should().BeEmpty("the viewer cannot see the only thing that matches");

    /// <summary>An article block added to an entry by the given user, keeping the blocks they can see.</summary>
    private async Task<Guid> AddBlock(World world, Users who, Guid entryId, string text, Visibility visibility)
    {
        fixture.LoginAsUser(who);
        var before = await fixture.GetEntry(world.Id, entryId);
        before.Should().Succeed();
        var after = await fixture.PutEntryArticle(world.Id, entryId, before.Value.Article.Etag,
        [
            .. before.Value.Article.Blocks.Select(BlockEdit.Keep),
            new BlockEdit(null, text, visibility),
        ]);
        after.Should().Succeed();
        after.Value.Article.Blocks.Should().HaveCount(before.Value.Article.Blocks.Length + 1);
        return after.Value.Article.Blocks[^1].Id;
    }

    private async Task<Guid> Entry(World world, Users who, string name, Visibility visibility = Visibility.Everyone)
    {
        fixture.LoginAsUser(who);
        var entry = await fixture.PostEntry(world.Id, name, EntryKind.Character, visibility);
        entry.Should().Succeed();
        return entry.Value.Id;
    }

    private async Task<Guid> Note(World world, Users who, string text, Visibility visibility = Visibility.Everyone)
    {
        fixture.LoginAsUser(who);
        var note = await fixture.PostSessionNote(world.Id, text, visibility);
        note.Should().Succeed();
        return note.Value.Id;
    }

    // 1. A DM note: the author and the DMs find it, a player does not.

    [Fact]
    public async Task ADmNote_IsFoundByItsAuthorAndTheDms_AndByNoPlayer()
    {
        var world = await NewWorld("Leak: DM note");
        var noteId = await Note(world, Users.DM, $"The bandits answer to {Token} of the north", Visibility.DM);

        var author = OneNote(await Search(world, Users.DM, Token), SearchSectionKey.Notes, noteId);
        author.Visibility.Should().Be(Visibility.DM);
        author.Images.Should().BeEmpty();
        OneNote(await Search(world, Users.Outsider, Token), SearchSectionKey.Notes, noteId);

        Nothing(await Search(world, Users.Player, Token));
        Nothing(await Search(world, Users.Player2, Token));
    }

    // 2. A Me note: only its author, not even a DM.

    [Fact]
    public async Task AMeNote_IsFoundOnlyByItsAuthor()
    {
        var world = await NewWorld("Leak: Me note");
        var noteId = await Note(world, Users.Player, $"I still do not trust {Token}", Visibility.Me);

        OneNote(await Search(world, Users.Player, Token), SearchSectionKey.Notes, noteId).Visibility
            .Should().Be(Visibility.Me);

        Nothing(await Search(world, Users.DM, Token));
        Nothing(await Search(world, Users.Outsider, Token));
        Nothing(await Search(world, Users.Player2, Token));
    }

    // 3. A hidden Everyone note: the author and the DMs, not another player.

    [Fact]
    public async Task AHiddenNote_IsFoundByItsAuthorAndTheDms_AndNotByAnotherPlayer()
    {
        var world = await NewWorld("Leak: hidden note");
        var noteId = await Note(world, Users.Player, $"{Token} was at the table, I swear it");

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionNoteHidden(world.Id, noteId, true)).Should().Succeed();

        OneNote(await Search(world, Users.Player, Token), SearchSectionKey.Notes, noteId);
        OneNote(await Search(world, Users.DM, Token), SearchSectionKey.Notes, noteId);
        OneNote(await Search(world, Users.Outsider, Token), SearchSectionKey.Notes, noteId);

        Nothing(await Search(world, Users.Player2, Token));
    }

    // 4. A DM image note's caption: absent from a player's Images.

    [Fact]
    public async Task ADmImageNotesCaption_IsAbsentFromAPlayersImages()
    {
        var world = await NewWorld("Leak: DM image note");
        fixture.LoginAsUser(Users.DM);
        var image = await fixture.UploadFixture(world.Id, ImageFixtures.Alpha);
        var note = await fixture.PostImageNote(world.Id, $"The ledger {Token} kept", [image.Id], Visibility.DM);
        note.Should().Succeed();

        var dm = await Search(world, Users.DM, Token);
        OneNote(dm, SearchSectionKey.Images, note.Value.Id).Images.Should().ContainSingle();
        dm.Section(SearchSectionKey.Notes).Should().BeEmpty("an image note appears once, under Images");
        OneNote(await Search(world, Users.Outsider, Token), SearchSectionKey.Images, note.Value.Id);

        Nothing(await Search(world, Users.Player, Token));
        Nothing(await Search(world, Users.Player2, Token));
    }

    // 5. A DM entry's name and one of its aliases: no entry hit for a player.

    [Fact]
    public async Task ADmEntrysNameAndAlias_GiveNoEntryHitForAPlayer()
    {
        var world = await NewWorld("Leak: DM entry");
        var entryId = await Entry(world, Users.DM, $"{Token} Gravemane", Visibility.DM);
        (await fixture.PutEntryAliases(world.Id, entryId, "Gravemane the Pale")).Should().Succeed();

        OneEntry(await Search(world, Users.DM, Token), entryId).MatchedOn.Should().Be(SearchMatchedOn.Name);
        var alias = OneEntry(await Search(world, Users.Outsider, "gravemane"), entryId);
        alias.MatchedOn.Should().Be(SearchMatchedOn.Alias);
        alias.Alias.Should().Be("Gravemane the Pale");

        Nothing(await Search(world, Users.Player, Token));
        Nothing(await Search(world, Users.Player, "gravemane"));
        Nothing(await Search(world, Users.Player2, Token));
    }

    // 6. A Me entry: only its creator.

    [Fact]
    public async Task AMeEntry_IsFoundOnlyByItsCreator()
    {
        var world = await NewWorld("Leak: Me entry");
        var entryId = await Entry(world, Users.Player, $"{Token} the Unseen", Visibility.Me);

        OneEntry(await Search(world, Users.Player, Token), entryId).MatchedOn.Should().Be(SearchMatchedOn.Name);

        Nothing(await Search(world, Users.DM, Token));
        Nothing(await Search(world, Users.Outsider, Token));
        Nothing(await Search(world, Users.Player2, Token));
    }

    // 7. A 🔒 DM block in an Everyone entry: no entry hit for a player, a snippet for the DM.

    [Fact]
    public async Task ADmBlockInAnEveryoneEntry_GivesNoHitForAPlayer_AndGivesTheDmsASnippet()
    {
        var world = await NewWorld("Leak: DM block");
        var entryId = await Entry(world, Users.Player, "Halia Thornton");
        var blockId = await AddBlock(world, Users.DM, entryId, $"{Token} pays the Redbrands in silver", Visibility.DM);

        foreach (var dm in new[] { Users.DM, Users.Outsider })
        {
            var hit = OneEntry(await Search(world, dm, Token), entryId);
            hit.MatchedOn.Should().Be(SearchMatchedOn.Article);
            hit.BlockId.Should().Be(blockId);
            hit.Snippet!.Text.Should().Contain(Token);
            Highlighted(hit.Snippet!).Should().Be(Token);
        }

        Nothing(await Search(world, Users.Player, Token));
        Nothing(await Search(world, Users.Player2, Token));
    }

    // 8. A 🔒 Me block: only its owner, not another DM.

    [Fact]
    public async Task AMeBlock_IsFoundOnlyByItsOwner()
    {
        var world = await NewWorld("Leak: Me block");
        var entryId = await Entry(world, Users.Player, "Daran Edermath");
        var blockId = await AddBlock(world, Users.DM, entryId, $"My own note: {Token} is the patron", Visibility.Me);

        var owner = OneEntry(await Search(world, Users.DM, Token), entryId);
        owner.BlockId.Should().Be(blockId);
        owner.Snippet!.Text.Should().Contain(Token);

        Nothing(await Search(world, Users.Outsider, Token));
        Nothing(await Search(world, Users.Player, Token));
        Nothing(await Search(world, Users.Player2, Token));
    }

    // 9. A quote promoted from a DM note: absent for a player.

    [Fact]
    public async Task AQuotePromotedFromADmNote_IsAbsentForAPlayer()
    {
        var world = await NewWorld("Leak: promoted quote");
        var entryId = await Entry(world, Users.Player, "Tresendar Manor", Visibility.Everyone);

        fixture.LoginAsUser(Users.DM);
        var noteId = await Note(world, Users.DM, $"{Token} keeps a ledger in the cellar", Visibility.DM);
        var quote = await fixture.PostEntryQuote(world.Id, entryId, noteId);
        quote.Should().Succeed();

        var dm = await Search(world, Users.Outsider, Token);
        OneEntry(dm, entryId).Snippet!.Text.Should().Contain(Token);
        dm.Section(SearchSectionKey.Notes).Should().ContainSingle();

        Nothing(await Search(world, Users.Player, Token));
        Nothing(await Search(world, Users.Player2, Token));
    }

    // 10. An ordinary block in a DM entry: absent for a player, because the entry's audience applies too.

    [Fact]
    public async Task AnOrdinaryBlockInADmEntry_IsAbsentForAPlayer()
    {
        var world = await NewWorld("Leak: block in a DM entry");
        var entryId = await Entry(world, Users.DM, "Cragmaw Hideout", Visibility.DM);
        var blockId = await AddBlock(world, Users.DM, entryId, $"The goblins here work for {Token}", Visibility.Everyone);

        var hit = OneEntry(await Search(world, Users.Outsider, Token), entryId);
        hit.MatchedOn.Should().Be(SearchMatchedOn.Article);
        hit.BlockId.Should().Be(blockId);

        Nothing(await Search(world, Users.Player, Token));
        Nothing(await Search(world, Users.Player2, Token));
    }

    // 11. A mixed article: the player's snippet comes from the visible block and holds nothing of the secret one.

    [Fact]
    public async Task AMixedArticle_GivesThePlayerASnippetCutFromTheVisibleBlockOnly()
    {
        var world = await NewWorld("Leak: mixed article");
        const string VisibleText = "The " + Token + " caravan reached Neverwinter before dawn";
        const string SecretText = Token + " poisoned the cistern beneath Tresendar Manor";

        var entryId = await Entry(world, Users.Player, "The Caravan");
        var visibleBlockId = await AddBlock(world, Users.Player, entryId, VisibleText, Visibility.Everyone);
        var secretBlockId = await AddBlock(world, Users.DM, entryId, SecretText, Visibility.DM);

        var player = OneEntry(await Search(world, Users.Player, Token), entryId);
        player.MatchedOn.Should().Be(SearchMatchedOn.Article);
        player.BlockId.Should().Be(visibleBlockId, "the only block the player can see that matches");
        player.Snippet!.Text.Should().NotBeEmpty();
        VisibleText.Should().Contain(player.Snippet!.Text, "the snippet is cut from the visible block's text");
        foreach (var word in new[] { "poisoned", "cistern", "beneath", "Tresendar", "Manor" })
        {
            player.Snippet!.Text.Should().NotContain(word, "nothing from the secret block may appear");
        }
        Highlighted(player.Snippet!).Should().Be(Token);

        // The DM sees both blocks, and the snippet is cut from one of them, never stitched across.
        var dm = OneEntry(await Search(world, Users.DM, Token), entryId);
        new[] { visibleBlockId, secretBlockId }.Should().Contain(dm.BlockId!.Value);
        (dm.BlockId == visibleBlockId ? VisibleText : SecretText).Should().Contain(dm.Snippet!.Text);
    }

    // 12. Ranking: a DM note cannot move a player's order or hasMore.

    [Fact]
    public async Task ADmNote_MovesNeitherThePlayersOrderNorHasMore()
    {
        var world = await NewWorld("Leak: ranking");
        var first = await Note(world, Users.Player, $"We heard the name {Token} in the market");
        var second = await Note(world, Users.Player2, $"{Token} again, this time at the gate");

        var beforeOrder = (await Search(world, Users.Player, Token, take: 5)).Section(SearchSectionKey.Notes)
            .Select(h => h.Note!.Id).ToList();
        beforeOrder.Should().BeEquivalentTo(new[] { first, second }, "both Everyone notes match");
        var beforeSection = (await Search(world, Users.Player, Token, take: 1)).Sections
            .Should().ContainSingle(s => s.Key == SearchSectionKey.Notes).Subject;
        var beforeTop = beforeSection.Hits.Single().Note!.Id;

        // A DM note that repeats the token twenty times: the loudest possible row, invisible to
        // the player. ts_rank_cd and similarity score one row alone and Postgres keeps no corpus
        // statistics, so it cannot move a visible row either.
        await Note(world, Users.DM, string.Join(' ', Enumerable.Repeat(Token, 20)), Visibility.DM);

        var afterOrder = (await Search(world, Users.Player, Token, take: 5)).Section(SearchSectionKey.Notes)
            .Select(h => h.Note!.Id).ToList();
        var afterSection = (await Search(world, Users.Player, Token, take: 1)).Sections
            .Should().ContainSingle(s => s.Key == SearchSectionKey.Notes).Subject;

        afterOrder.Should().Equal(beforeOrder, "the player's order is the same as before the DM note");
        afterSection.HasMore.Should().Be(beforeSection.HasMore, "hasMore counts the rows the viewer can see");
        afterSection.Hits.Single().Note!.Id.Should().Be(beforeTop);

        // And the DM does see it, at the top: the note exists, it is just not the player's.
        (await Search(world, Users.DM, Token, take: 5)).Section(SearchSectionKey.Notes)
            .Should().HaveCount(3);
    }

    // 13. Counts: an entry hit's mentionCount is the viewer's own, the one GET entries gives them.

    [Fact]
    public async Task AnEntryHitsMentionCount_IsTheViewersOwnCount()
    {
        var world = await NewWorld("Leak: counts");
        var entryId = await Entry(world, Users.Player, $"{Token} Emberfall");
        var mention = new NewEntry(entryId, $"{Token} Emberfall");

        await Note(world, Users.Player, $"{mention.Mention} met us at the inn");
        await Note(world, Users.DM, $"{mention.Mention} is lying about the mine", Visibility.DM);

        foreach (var who in new[] { Users.Player, Users.DM })
        {
            fixture.LoginAsUser(who);
            var listed = (await fixture.GetEntries(world.Id)).Value.Entries.Single(e => e.Entry.Id == entryId);
            OneEntry(await Search(world, who, Token, sections: "entries"), entryId)
                .MentionCount.Should().Be(listed.MentionCount, $"{who}'s count is their own");
        }

        fixture.LoginAsUser(Users.Player);
        var playerCount = OneEntry(await Search(world, Users.Player, Token, sections: "entries"), entryId).MentionCount;
        var dmCount = OneEntry(await Search(world, Users.DM, Token, sections: "entries"), entryId).MentionCount;
        playerCount.Should().Be(1);
        dmCount.Should().Be(2, "the DM note mentioning it counts for a DM only");
    }

    // 14. A merged entry never appears as itself; its old name finds its target as an alias.

    [Fact]
    public async Task AMergedEntry_NeverAppearsAsItself_AndItsNameFindsItsTarget()
    {
        var world = await NewWorld("Leak: merge");
        var merged = await Entry(world, Users.Player, $"{Token} Emberfall");
        var target = await Entry(world, Users.Player, "Quelline Alderleaf");

        fixture.LoginAsUser(Users.DM);
        (await fixture.PostEntryMerge(world.Id, merged, target)).Should().Succeed();

        foreach (var who in new[] { Users.Player, Users.DM, Users.Outsider, Users.Player2 })
        {
            var hit = OneEntry(await Search(world, who, Token, sections: "entries"), target);
            hit.MatchedOn.Should().Be(SearchMatchedOn.Alias);
            hit.Alias.Should().Be($"{Token} Emberfall");
            hit.Entry.Id.Should().NotBe(merged, "a merged entry is never a hit of its own");
        }
    }

    // 15. Markup: no snippet holds `entry:` or a Guid.

    [Fact]
    public async Task NoSnippet_HoldsAMentionsDestination()
    {
        var world = await NewWorld("Leak: markup");
        var entryId = await Entry(world, Users.Player, "The Old Dwarf");
        var mention = new NewEntry(entryId, "the old dwarf");

        fixture.LoginAsUser(Users.Player);
        (await fixture.PostSessionNote(
            world.Id, $"We met {mention.Mention} near {Token}'s camp and \\*nothing\\* was said"))
            .Should().Succeed();
        await AddBlock(world, Users.Player, entryId, $"{mention.Mention} guided us past {Token} ridge", Visibility.Everyone);

        // Not only the mentions the client writes. Note and block text is length-validated and
        // nothing more, so a member can type a destination by hand or mistype a mention until only
        // its tail is left. PlainText takes a bare "(entry:<id>)" out unconditionally, whatever is
        // around it, which is what makes this case about what a member can type rather than about
        // what the composer happens to produce.
        (await fixture.PostSessionNote(
            world.Id,
            $"{Token} left a note (entry:{entryId}) and a broken @[a]b](entry:{entryId}) "
                + $"and an escaped \\(entry:{entryId}\\) too"))
            .Should().Succeed();
        await AddBlock(
            world, Users.Player, entryId,
            $"{Token} ridge again, hand-typed (entry:{entryId}) and malformed @[x](entry:{entryId}",
            Visibility.Everyone);

        foreach (var query in new[] { Token, "dwarf", "old dwarf" })
        foreach (var who in new[] { Users.Player, Users.DM })
        {
            var snippets = (await Search(world, who, query)).Sections
                .SelectMany(s => s.Hits)
                .SelectMany(h => new[] { h.Note?.Snippet, h.Entry?.Snippet, h.Session?.Snippet })
                .Where(s => s is not null)
                .Select(s => s!.Text)
                .ToList();
            snippets.Should().NotBeEmpty($"'{query}' finds something for {who}");
            foreach (var text in snippets)
            {
                text.Should().NotContain("entry:");
                text.Should().NotMatchRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}");
                text.Should().NotContain("\\", "backslash escapes are removed");
            }
        }
    }

    // 16. Campaigns: another campaign's member finds nothing here, and a non-member gets a 403.

    [Fact]
    public async Task AnotherCampaignsMember_FindsNothingHere_AndANonMemberGetsA403()
    {
        var world = await NewWorld("Leak: campaigns");
        var noteId = await Note(world, Users.Player, $"{Token} is here, in this campaign only");
        OneNote(await Search(world, Users.Player, Token), SearchSectionKey.Notes, noteId);

        // The stranger's own campaign, with its own session and a note of its own, so the search
        // there has something to answer with and still does not answer with ours.
        fixture.LoginAsUser(Users.Stranger);
        var elsewhere = await fixture.PostCreateCampaign(new() { Name = "Leak: elsewhere" });
        elsewhere.Should().Succeed();
        (await fixture.PostStartSession(elsewhere.Value.Id, 1)).Should().Succeed();
        (await fixture.PostSessionNote(elsewhere.Value.Id, "Nothing to do with the other lot")).Should().Succeed();

        var theirs = await fixture.GetSearch(elsewhere.Value.Id, Token);
        theirs.Should().Succeed();
        theirs.Value.Sections.Should().BeEmpty("their campaign holds no such word");

        (await fixture.GetStatus(SearchUrl(world.Id, Token))).Should().Be(403, "a non-member gets 403, as on every campaign route");
        // An invalid query is still a 403 and not a 400: membership is checked first, so an
        // outsider never learns whether their query was valid.
        (await fixture.GetStatus(SearchUrl(world.Id, ""))).Should().Be(403);
    }
}
