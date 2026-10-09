using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Search;

/// <summary>
/// A search straight after each write, with <b>no wait anywhere</b> (17a step 14). There is no
/// stored search table: every source is an inline projection saved in the same transaction as its
/// event, and Postgres maintains the expression indexes in that transaction, so a search sees each
/// post, edit, hide, narrowing, visibility change, rename, article edit, merge and delete as soon
/// as it commits.
/// <para>
/// If any case here needed a delay to pass, the design's central claim ("consistency for free")
/// would be wrong. That is why not one of these tests sleeps, polls or retries.
/// </para>
/// </summary>
public class SearchConsistencyTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private const string Token = "zanthor";
    private const string OtherToken = "quelline";

    private async Task<TestCampaign> NewCampaign(string name)
        => await TestCampaign.Create(fixture, name, withSecondPlayer: false);

    private async Task<SearchResponse> Search(Guid campaignId, Users who, string q, string? sections = null)
    {
        fixture.LoginAsUser(who);
        var response = await fixture.GetSearch(campaignId, q, sections);
        response.Should().Succeed();
        return response.Value;
    }

    /// <summary>The note ids a viewer finds, over Notes and Images.</summary>
    private async Task<Guid[]> NotesFound(Guid campaignId, Users who, string q)
    {
        var response = await Search(campaignId, who, q);
        return
        [
            .. response.Section(SearchSectionKey.Notes).Concat(response.Section(SearchSectionKey.Images))
                .Select(h => h.Note!.Id),
        ];
    }

    private async Task<Guid[]> EntriesFound(Guid campaignId, Users who, string q)
        => [.. (await Search(campaignId, who, q, sections: "entries"))
            .Section(SearchSectionKey.Entries).Select(h => h.Entry!.Entry.Id)];

    [Fact]
    public async Task APostedNote_IsFoundAtOnce()
    {
        var campaign = await NewCampaign("Consistency: post");
        fixture.LoginAsUser(Users.Player);
        var note = await fixture.PostSessionNote(campaign.Id, $"{Token} rode north with the wagons");
        note.Should().Succeed();

        (await NotesFound(campaign.Id, Users.Player, Token)).Should().Equal(note.Value.Id);
    }

    [Fact]
    public async Task AnEditedNote_IsFoundByItsNewTextAndNotByItsOldTextAtOnce()
    {
        var campaign = await NewCampaign("Consistency: edit");
        fixture.LoginAsUser(Users.Player);
        var note = await fixture.PostSessionNote(campaign.Id, "The wagons went north");
        note.Should().Succeed();
        (await NotesFound(campaign.Id, Users.Player, Token)).Should().BeEmpty();

        (await fixture.PutSessionNote(campaign.Id, note.Value.Id, $"The wagons went north with {Token}"))
            .Should().Succeed();
        (await NotesFound(campaign.Id, Users.Player, Token)).Should().BeEquivalentTo(new[] { note.Value.Id }, "the token appears");

        (await fixture.PutSessionNote(campaign.Id, note.Value.Id, $"The wagons went north with {OtherToken}"))
            .Should().Succeed();
        (await NotesFound(campaign.Id, Users.Player, Token)).Should().BeEmpty("the token is gone");
        (await NotesFound(campaign.Id, Users.Player, OtherToken)).Should().Equal(note.Value.Id);
    }

    [Fact]
    public async Task ADeletedNote_IsGoneAtOnce()
    {
        var campaign = await NewCampaign("Consistency: delete");
        fixture.LoginAsUser(Users.Player);
        var note = await fixture.PostSessionNote(campaign.Id, $"{Token} was here");
        note.Should().Succeed();
        (await NotesFound(campaign.Id, Users.Player, Token)).Should().Equal(note.Value.Id);

        (await fixture.DeleteSessionNote(campaign.Id, note.Value.Id)).Should().Succeed();
        (await NotesFound(campaign.Id, Users.Player, Token)).Should().BeEmpty();
        (await NotesFound(campaign.Id, Users.DM, Token)).Should().BeEmpty("a delete is a delete, even for a DM");
    }

    [Fact]
    public async Task ANoteNarrowedToDm_LeavesAPlayersResultsAtOnce()
    {
        var campaign = await NewCampaign("Consistency: narrow");
        fixture.LoginAsUser(Users.Player);
        var note = await fixture.PostSessionNote(campaign.Id, $"{Token} paid for the ale");
        note.Should().Succeed();
        (await NotesFound(campaign.Id, Users.DM, Token)).Should().Equal(note.Value.Id);

        // The author narrows their own note. The DM keeps it; nobody else has it.
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutSessionNoteVisibility(campaign.Id, note.Value.Id, Visibility.Me)).Should().Succeed();

        (await NotesFound(campaign.Id, Users.DM, Token)).Should().BeEmpty("a Me note is its author's alone");
        (await NotesFound(campaign.Id, Users.Player, Token)).Should().Equal(note.Value.Id);
    }

    [Fact]
    public async Task AHiddenNote_IsGoneForAPlayerAtOnce_AndBackWhenUnhidden()
    {
        var campaign = await NewCampaign("Consistency: hide");
        fixture.LoginAsUser(Users.Player);
        var note = await fixture.PostSessionNote(campaign.Id, $"{Token} at the gate");
        note.Should().Succeed();
        var player2 = await campaign.Join(fixture, Users.Player2);
        player2.Should().NotBeEmpty();
        (await NotesFound(campaign.Id, Users.Player2, Token)).Should().Equal(note.Value.Id);

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionNoteHidden(campaign.Id, note.Value.Id, true)).Should().Succeed();
        (await NotesFound(campaign.Id, Users.Player2, Token)).Should().BeEmpty("hidden, at once");
        (await NotesFound(campaign.Id, Users.DM, Token)).Should().BeEquivalentTo(new[] { note.Value.Id }, "a DM still sees it, marked");

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionNoteHidden(campaign.Id, note.Value.Id, false)).Should().Succeed();
        (await NotesFound(campaign.Id, Users.Player2, Token)).Should().BeEquivalentTo(new[] { note.Value.Id }, "unhidden, at once");
    }

    [Fact]
    public async Task AnEntrysVisibilityChange_IsFollowedAtOnce()
    {
        var campaign = await NewCampaign("Consistency: entry visibility");
        fixture.LoginAsUser(Users.Player);
        var entry = await fixture.PostEntry(campaign.Id, $"{Token} Emberfall");
        entry.Should().Succeed();
        (await EntriesFound(campaign.Id, Users.DM, Token)).Should().Equal(entry.Value.Id);

        fixture.LoginAsUser(Users.Player);
        (await fixture.PutEntryVisibility(campaign.Id, entry.Value.Id, Visibility.Me)).Should().Succeed();
        (await EntriesFound(campaign.Id, Users.DM, Token)).Should().BeEmpty("a Me entry is its creator's alone");
        (await EntriesFound(campaign.Id, Users.Player, Token)).Should().Equal(entry.Value.Id);

        fixture.LoginAsUser(Users.Player);
        (await fixture.PutEntryVisibility(campaign.Id, entry.Value.Id, Visibility.Everyone)).Should().Succeed();
        (await EntriesFound(campaign.Id, Users.DM, Token)).Should().Equal(entry.Value.Id);
    }

    [Fact]
    public async Task ARenamedEntry_IsFoundByItsNewNameAtOnce()
    {
        var campaign = await NewCampaign("Consistency: rename");
        fixture.LoginAsUser(Users.Player);
        var entry = await fixture.PostEntry(campaign.Id, $"{Token} Emberfall");
        entry.Should().Succeed();

        (await fixture.PutEntryName(campaign.Id, entry.Value.Id, $"{OtherToken} Alderleaf")).Should().Succeed();
        (await EntriesFound(campaign.Id, Users.Player, Token)).Should().BeEmpty("the old name is gone");
        (await EntriesFound(campaign.Id, Users.Player, OtherToken)).Should().Equal(entry.Value.Id);
    }

    [Fact]
    public async Task AnAliasAddedAndRemoved_IsFollowedAtOnce()
    {
        var campaign = await NewCampaign("Consistency: aliases");
        fixture.LoginAsUser(Users.Player);
        var entry = await fixture.PostEntry(campaign.Id, "Iarno Albrek");
        entry.Should().Succeed();
        (await EntriesFound(campaign.Id, Users.Player, Token)).Should().BeEmpty();

        (await fixture.PutEntryAliases(campaign.Id, entry.Value.Id, Token)).Should().Succeed();
        var hit = (await Search(campaign.Id, Users.Player, Token, sections: "entries"))
            .Section(SearchSectionKey.Entries).Should().ContainSingle().Subject.Entry!;
        hit.Entry.Id.Should().Be(entry.Value.Id);
        hit.MatchedOn.Should().Be(SearchMatchedOn.Alias);
        hit.Alias.Should().Be(Token);

        (await fixture.PutEntryAliases(campaign.Id, entry.Value.Id)).Should().Succeed();
        (await EntriesFound(campaign.Id, Users.Player, Token)).Should().BeEmpty("the alias is gone");
    }

    [Fact]
    public async Task AnArticleEditThatMakesABlockSecret_IsFollowedAtOnce()
    {
        var campaign = await NewCampaign("Consistency: secret block");
        fixture.LoginAsUser(Users.DM);
        var entry = await fixture.PostEntry(campaign.Id, "Tresendar Manor", EntryKind.Place);
        entry.Should().Succeed();
        var withBlock = await fixture.PutEntryArticle(campaign.Id, entry.Value.Id, entry.Value.Article.Etag,
            [new BlockEdit(null, $"The cellars were dug by {Token}")]);
        withBlock.Should().Succeed();
        var block = withBlock.Value.Article.Blocks.Single();

        (await EntriesFound(campaign.Id, Users.Player, Token)).Should().BeEquivalentTo(new[] { entry.Value.Id }, "an ordinary block");

        // The same block, now 🔒 DM. The player's hit goes with it, at once.
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryArticle(campaign.Id, entry.Value.Id, withBlock.Value.Article.Etag,
            [new BlockEdit(block.Id, block.Text, Visibility.DM)])).Should().Succeed();

        (await EntriesFound(campaign.Id, Users.Player, Token)).Should().BeEmpty("the text is now in a 🔒 block");
        (await EntriesFound(campaign.Id, Users.DM, Token)).Should().Equal(entry.Value.Id);
    }

    [Fact]
    public async Task AMergedEntry_IsFoundAsItsTargetAtOnce()
    {
        var campaign = await NewCampaign("Consistency: merge");
        fixture.LoginAsUser(Users.Player);
        var from = await fixture.PostEntry(campaign.Id, $"{Token} Emberfall");
        from.Should().Succeed();
        var into = await fixture.PostEntry(campaign.Id, "Quelline Alderleaf");
        into.Should().Succeed();
        (await EntriesFound(campaign.Id, Users.Player, Token)).Should().Equal(from.Value.Id);

        fixture.LoginAsUser(Users.DM);
        (await fixture.PostEntryMerge(campaign.Id, from.Value.Id, into.Value.Id)).Should().Succeed();

        var hit = (await Search(campaign.Id, Users.Player, Token, sections: "entries"))
            .Section(SearchSectionKey.Entries).Should().ContainSingle().Subject.Entry!;
        hit.Entry.Id.Should().Be(into.Value.Id, "the merged entry is found as its target, at once");
        hit.MatchedOn.Should().Be(SearchMatchedOn.Alias);
    }
}
