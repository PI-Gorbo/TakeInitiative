using System.Text.Json;
using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Search;

/// <summary>
/// One seeded campaign, every section, and one negative visibility case per unit kind. It proves
/// the SQL runs, the visibility fragments bite and the snippets come out: the exhaustive leak
/// matrix, the consistency cases and the SQL/C# parity table are their own suites.
/// <para>
/// The campaign has two DMs (the owner, and <see cref="Users.Outsider"/> promoted after joining)
/// and one player, so "another DM cannot see a <c>Me</c> entry" has someone to be.
/// </para>
/// </summary>
public class SearchSmokeTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>, IAsyncLifetime
{
    private TestCampaign _campaign = null!;
    private Guid _gundrenId;
    private Guid _ordinaryBlockId;
    private Guid _publicNoteId;
    private Guid _dmNoteId;
    private Guid _imageNoteId;
    private Guid _session2Id;
    private Guid _glasstaffId;

    private const string Title = "The Triboar Trail";

    public async Task InitializeAsync()
    {
        _campaign = await TestCampaign.Create(fixture, "Search smoke");

        // A second DM, so a `Me` entry has a DM who is not its creator.
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutMemberRole(_campaign.Id, _campaign.SecondPlayerMemberId!.Value, Role.DM)).Should().Succeed();

        var session2 = await fixture.PostStartSession(_campaign.Id, 2);
        session2.Should().Succeed();
        _session2Id = session2.Value.Id;
        (await fixture.PutSessionTitle(_campaign.Id, _session2Id, Title)).Should().Succeed();

        // An Everyone entry created by the player, with an alias, an ordinary block and a 🔒 DM
        // block owned by the DM.
        fixture.LoginAsUser(Users.Player);
        var entry = await fixture.PostEntry(_campaign.Id, "Gündren Rockseeker");
        entry.Should().Succeed();
        _gundrenId = entry.Value.Id;
        (await fixture.PutEntryAliases(_campaign.Id, _gundrenId, "Rockseeker")).Should().Succeed();
        var withOrdinary = await fixture.PutEntryArticle(_campaign.Id, _gundrenId, entry.Value.Article.Etag,
            [new BlockEdit(null, "A dwarf of the Rockseeker clan who hired the party in Neverwinter")]);
        withOrdinary.Should().Succeed();
        _ordinaryBlockId = withOrdinary.Value.Article.Blocks.Single().Id;

        fixture.LoginAsUser(Users.DM);
        var dmView = await fixture.GetEntry(_campaign.Id, _gundrenId);
        (await fixture.PutEntryArticle(_campaign.Id, _gundrenId, dmView.Value.Article.Etag,
        [
            BlockEdit.Keep(dmView.Value.Article.Blocks.Single()),
            new BlockEdit(null, "zanthor pays him", Visibility.DM),
        ])).Should().Succeed();

        // An Everyone note with a mention, a DM note, and an image note whose caption is its text.
        fixture.LoginAsUser(Users.Player);
        var mention = new NewEntry(_gundrenId, "Gündren Rockseeker");
        var publicNote = await fixture.PostSessionNote(_campaign.Id, $"We met {mention.Mention} on the road near Phandalin");
        publicNote.Should().Succeed();
        _publicNoteId = publicNote.Value.Id;

        var image = await fixture.UploadFixture(_campaign.Id, ImageFixtures.Alpha);
        var imageNote = await fixture.PostImageNote(_campaign.Id, "Letter from Gundren", [image.Id]);
        imageNote.Should().Succeed();
        _imageNoteId = imageNote.Value.Id;

        fixture.LoginAsUser(Users.DM);
        var dmNote = await fixture.PostSessionNote(_campaign.Id, "Gundren is working for zanthor", Visibility.DM);
        dmNote.Should().Succeed();
        _dmNoteId = dmNote.Value.Id;

        // A Me entry of the owning DM: not even the other DM sees it.
        var glasstaff = await fixture.PostEntry(_campaign.Id, "Glasstaff", EntryKind.Character, Visibility.Me);
        glasstaff.Should().Succeed();
        _glasstaffId = glasstaff.Value.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<SearchResponse> Search(Users who, string q, string? sections = null, int? take = null)
    {
        fixture.LoginAsUser(who);
        var response = await fixture.GetSearch(_campaign.Id, q, sections, take);
        response.Should().Succeed();
        return response.Value;
    }

    private static SearchEntryHit Entry(SearchResponse response)
        => response.Section(SearchSectionKey.Entries).Should().ContainSingle().Subject.Entry!;

    private static string Highlighted(Snippet snippet)
        => string.Join("|", snippet.Highlights.Select(h => snippet.Text.Substring(h.Start, h.Length)));

    [Fact]
    public async Task AnEntry_IsFoundByItsName()
    {
        var hit = Entry(await Search(Users.Player, "gundren", sections: "entries"));
        hit.Entry.Id.Should().Be(_gundrenId);
        hit.MatchedOn.Should().Be(SearchMatchedOn.Name);
        hit.Alias.Should().BeNull();
        hit.Snippet.Should().BeNull("a name hit has no snippet");
        // The accent is folded, as the @ picker folds it.
        hit.Entry.Name.Should().Be("Gündren Rockseeker");
    }

    [Fact]
    public async Task AnEntry_IsFoundByAnAlias_AndSaysWhichOneMatched()
    {
        var hit = Entry(await Search(Users.Player, "rocks", sections: "entries"));
        hit.Entry.Id.Should().Be(_gundrenId);
        hit.MatchedOn.Should().Be(SearchMatchedOn.Alias);
        hit.Alias.Should().Be("Rockseeker");
    }

    [Fact]
    public async Task AnEntry_IsFoundByItsArticleText_WithASnippetFromTheMatchingBlock()
    {
        var hit = Entry(await Search(Users.Player, "neverwinter", sections: "entries"));
        hit.Entry.Id.Should().Be(_gundrenId);
        hit.MatchedOn.Should().Be(SearchMatchedOn.Article);
        hit.BlockId.Should().Be(_ordinaryBlockId);
        hit.Snippet.Should().NotBeNull();
        Highlighted(hit.Snippet!).Should().Be("Neverwinter");
    }

    [Fact]
    public async Task ANote_IsFoundByItsText_WithTheMatchHighlighted()
    {
        var hits = (await Search(Users.Player, "phandalin")).Section(SearchSectionKey.Notes);
        var note = hits.Should().ContainSingle().Subject.Note!;
        note.Id.Should().Be(_publicNoteId);
        note.SessionNumber.Should().Be(2);
        Highlighted(note.Snippet).Should().Be("Phandalin");
        note.Images.Should().BeEmpty();
    }

    [Fact]
    public async Task AMention_IsSearchedByItsDisplayText_AndNoSnippetShowsItsDestination()
    {
        var note = (await Search(Users.Player, "rockseeker")).Section(SearchSectionKey.Notes)
            .Select(h => h.Note!).Single(n => n.Id == _publicNoteId);
        note.Snippet.Text.Should().NotContain("entry:");
        note.Snippet.Text.Should().NotMatchRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}");
        // ShortWord=2 trims the leading "We" off the fragment, so this is the text from the match on.
        note.Snippet.Text.Should().Be("met Gündren Rockseeker on the road near Phandalin");
    }

    [Fact]
    public async Task NoteText_IsNotAccentFolded_SoTheEntryIsFoundAndTheNoteIsNot()
    {
        // Notes, "Accents": names and aliases fold (the entry hit below), note and block text do
        // not, because unaccent is not immutable and so cannot be in an index expression. The
        // note says "Gündren"; "gundren" finds the entry and not the note.
        var response = await Search(Users.Player, "gundren");
        Entry(response).Entry.Id.Should().Be(_gundrenId);
        response.Section(SearchSectionKey.Notes).Select(h => h.Note!.Id).Should().NotContain(_publicNoteId);
    }

    [Fact]
    public async Task AnImageNote_IsFoundByItsCaption_InImagesAndNotInNotes()
    {
        var response = await Search(Users.Player, "letter");
        var hit = response.Section(SearchSectionKey.Images).Should().ContainSingle().Subject.Note!;
        hit.Id.Should().Be(_imageNoteId);
        hit.Images.Should().ContainSingle();
        Highlighted(hit.Snippet).Should().Be("Letter");
        response.Section(SearchSectionKey.Notes).Should().BeEmpty("an image note appears once, under Images");
    }

    [Fact]
    public async Task ASession_IsFoundByItsNumber_WithNoSnippet()
    {
        var hit = (await Search(Users.Player, "s2")).Section(SearchSectionKey.Sessions)
            .Should().ContainSingle().Subject.Session!;
        hit.Session.Id.Should().Be(_session2Id);
        hit.Snippet.Should().BeNull("a number match does not echo the title back");
    }

    [Fact]
    public async Task ASession_IsFoundByAWordOfItsTitle()
    {
        var hit = (await Search(Users.Player, "triboar")).Section(SearchSectionKey.Sessions)
            .Should().ContainSingle().Subject.Session!;
        hit.Session.Title.Should().Be(Title);
        Highlighted(hit.Snippet!).Should().Be("Triboar");
    }

    [Fact]
    public async Task ADmNote_IsAbsentForAPlayer_AndPresentForADm()
    {
        var player = await Search(Users.Player, "zanthor");
        player.Section(SearchSectionKey.Notes).Should().BeEmpty();
        player.Sections.Should().BeEmpty("nothing in the campaign mentions zanthor where a player can see it");

        var dm = await Search(Users.DM, "zanthor");
        dm.Section(SearchSectionKey.Notes).Select(h => h.Note!.Id).Should().Contain(_dmNoteId);
    }

    [Fact]
    public async Task ASecretBlock_GivesNoEntryHitForAPlayer_AndGivesTheDmASnippet()
    {
        (await Search(Users.Player, "zanthor", sections: "entries"))
            .ShouldHaveNoSection(SearchSectionKey.Entries, "the only block holding the word is 🔒 DM");
        // The control: the same viewer, the same section, a word they can see. So the absence above
        // is the block's audience and not Entries going unasked or unanswered for this player.
        (await Search(Users.Player, "gundren", sections: "entries"))
            .ShouldHaveSection(SearchSectionKey.Entries, "the player does get entry hits").Should().ContainSingle();

        var hit = Entry(await Search(Users.DM, "zanthor", sections: "entries"));
        hit.Entry.Id.Should().Be(_gundrenId);
        hit.MatchedOn.Should().Be(SearchMatchedOn.Article);
        Highlighted(hit.Snippet!).Should().Be("zanthor");
    }

    [Fact]
    public async Task AMeEntry_IsAbsentForAnotherDm_AndPresentForItsCreator()
    {
        (await Search(Users.Outsider, "glasstaff", sections: "entries"))
            .Section(SearchSectionKey.Entries).Should().BeEmpty("the other DM is not its creator");

        Entry(await Search(Users.DM, "glasstaff", sections: "entries")).Entry.Id.Should().Be(_glasstaffId);
    }

    [Fact]
    public async Task AnEntriesPrefix_ReturnsEntriesOnly()
    {
        var response = await Search(Users.Player, "@gundren");
        response.Query.Should().Be("gundren");
        response.Sections.Select(s => s.Key).Should().Equal(SearchSectionKey.Entries);
    }

    [Fact]
    public async Task AFuzzyQuery_FindsTheName()
        => Entry(await Search(Users.Player, "gundrn", sections: "entries")).Entry.Id.Should().Be(_gundrenId);

    [Theory]
    [InlineData("", null, null, GetSearch.QueryErrorKey)]
    [InlineData("gundren", "monsters", null, GetSearch.SectionsErrorKey)]
    // A section is named, never numbered: "99" is no section at all, "0" is not a way to say
    // entries, and "-1" is neither. All three used to be 200s (17a.11).
    [InlineData("gundren", "99", null, GetSearch.SectionsErrorKey)]
    [InlineData("gundren", "0", null, GetSearch.SectionsErrorKey)]
    [InlineData("gundren", "-1", null, GetSearch.SectionsErrorKey)]
    [InlineData("gundren", "entries,99", null, GetSearch.SectionsErrorKey)]
    [InlineData("gundren", null, 0, GetSearch.TakeErrorKey)]
    [InlineData("gundren", null, 21, GetSearch.TakeErrorKey)]
    public async Task ABadRequest_IsA400_UnderItsOwnErrorKey(string q, string? sections, int? take, string key)
    {
        fixture.LoginAsUser(Users.Player);
        var result = await fixture.AlbaHost.Scenario(_ =>
        {
            _.Get.Url(SearchUrl(_campaign.Id, q, sections, take));
            _.StatusCodeShouldBe(400);
        });
        JsonDocument.Parse(await result.ReadAsTextAsync()).RootElement
            .GetProperty("errors").TryGetProperty(key, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("'")]
    [InlineData("a & b | !c")]
    [InlineData(":*")]
    [InlineData("\\")]
    public async Task TsQuerySyntax_IsSearchedAsText_AndIsA200(string q)
        => (await Search(Users.Player, q)).Query.Should().Be(q);

    [Fact]
    public async Task AnEntryHitCarriesTheViewersOwnMentionCount()
    {
        // The player's count comes from notes they can see: the two of their own, not the DM's.
        var player = Entry(await Search(Users.Player, "gundren", sections: "entries"));
        var listed = (await fixture.GetEntries(_campaign.Id)).Value.Entries.Single(e => e.Entry.Id == _gundrenId);
        player.MentionCount.Should().Be(listed.MentionCount);
    }
}
