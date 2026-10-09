using System.Text.Json;
using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// Promote (15e.5): a note, or part of it, becomes a quote at the end of an article, with the
/// note's audience. <see cref="Users.Player"/> writes the notes and creates the entry,
/// <see cref="Users.DM"/> is the DM and <see cref="Users.Outsider"/> the other player.
/// </summary>
public class PromoteTests(RecordingHubFixture fixture) : IClassFixture<RecordingHubFixture>
{
    private async Task<(TestCampaign Campaign, EntryResponse Entry)> NewEntry(string name)
    {
        var campaign = await TestCampaign.Create(fixture, name);
        fixture.LoginAsUser(Users.Player);
        var entry = (await fixture.PostEntry(campaign.Id, "Gundren")).Value;
        return (campaign, entry);
    }

    private async Task<SessionNoteResponse> Note(Guid campaignId, string text, Visibility visibility = Visibility.Everyone)
    {
        fixture.LoginAsUser(Users.Player);
        var note = await fixture.PostSessionNote(campaignId, text, visibility);
        note.Should().Succeed();
        return note.Value;
    }

    private async Task<EntryQuoteResponse> Promote(Users user, Guid campaignId, Guid entryId, Guid noteId, string? text = null)
    {
        fixture.LoginAsUser(user);
        var promoted = await fixture.PostEntryQuote(campaignId, entryId, noteId, text);
        promoted.Should().Succeed();
        return promoted.Value;
    }

    private async Task<ArticleBlockResponse[]> Blocks(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        return (await fixture.GetEntry(campaignId, entryId)).Value.Article.Blocks;
    }

    [Fact]
    public async Task TheWholeNote_IsQuoted_WithItsLinkFields()
    {
        var (campaign, entry) = await NewEntry("Promote whole");
        var note = await Note(campaign.Id, "We met Gundren on the road.\n\nHe hired us.");
        var sessions = (await fixture.GetSessions(campaign.Id)).Value;

        var promoted = await Promote(Users.DM, campaign.Id, entry.Id, note.Id);

        var quote = promoted.Entry.Article.Blocks.Should().ContainSingle().Subject;
        quote.Id.Should().Be(promoted.BlockId);
        quote.Text.Should().Be(note.Text);
        quote.Visibility.Should().Be(Visibility.Everyone);
        quote.OwnerMemberId.Should().Be(campaign.PlayerMemberId, "a quote is owned by the note's author");
        quote.Quote.Should().NotBeNull();
        quote.Quote!.NoteId.Should().Be(note.Id);
        quote.Quote.SessionId.Should().Be(note.SessionId);
        quote.Quote.SessionNumber.Should().Be(sessions.Sessions.Single(s => s.Id == note.SessionId).Number);
        quote.Quote.AuthorMemberId.Should().Be(campaign.PlayerMemberId);
        quote.Quote.PromotedByMemberId.Should().Be(campaign.DmMemberId);
        quote.Quote.PromotedAt.Ticks.Should().Be(quote.Quote.PromotedAt.Ticks / 10 * 10, "timestamps are kept to the microsecond");

        var @event = (await TestCampaign.EventsOf(fixture, entry.Id)).Last();
        ((EntryQuotePromoted)@event.Data).Actor.MemberId.Should().Be(campaign.DmMemberId);
        @event.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ASubstring_IsQuoted_EvenWithDifferentWhitespace_AndGoesLast()
    {
        var (campaign, entry) = await NewEntry("Promote substring");
        fixture.LoginAsUser(Users.Player);
        await fixture.PutEntryArticle(campaign.Id, entry.Id, entry.Article.Etag, [new BlockEdit(null, "Dwarf prospector.")]);
        var note = await Note(campaign.Id, "We met **Gundren**\non the road. He hired us.");

        var promoted = await Promote(Users.Outsider, campaign.Id, entry.Id, note.Id, "  **Gundren** on the\n  road.  ");

        promoted.Entry.Article.Blocks.Select(b => b.Text).Should().Equal("Dwarf prospector.", "**Gundren** on the\n  road.");
    }

    [Fact]
    public async Task TextThatIsNotFromTheNote_IsA400()
    {
        var (campaign, entry) = await NewEntry("Promote not a substring");
        var note = await Note(campaign.Id, "We met Gundren on the road.");

        fixture.LoginAsUser(Users.DM);
        var (status, body) = await fixture.Send(HttpMethod.Post, QuotesUrl(campaign.Id, entry.Id), new { noteId = note.Id, text = "We met Klarg" });

        status.Should().Be(400);
        JsonDocument.Parse(body).RootElement.GetProperty("errors").GetProperty(PostEntryQuote.TextErrorKey)[0].GetString()
            .Should().Be("A quote must be text from the note.");
        (await Blocks(Users.DM, campaign.Id, entry.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task ANoteTheCallerCannotSee_IsA404_LikeOneThatDoesNotExist()
    {
        var (campaign, entry) = await NewEntry("Promote hidden note");
        var dmNote = await Note(campaign.Id, "Glasstaff is Iarno.", Visibility.DM);
        var meNote = await Note(campaign.Id, "I owe Gundren gold.", Visibility.Me);
        var hidden = await Note(campaign.Id, "Rude joke about Gundren.");
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionNoteHidden(campaign.Id, hidden.Id, true)).Should().Succeed();

        fixture.LoginAsUser(Users.Outsider);
        foreach (var noteId in new[] { dmNote.Id, meNote.Id, hidden.Id, Guid.NewGuid() })
        {
            (await fixture.Send(HttpMethod.Post, QuotesUrl(campaign.Id, entry.Id), new { noteId })).Status.Should().Be(404);
        }
        fixture.LoginAsUser(Users.DM);
        (await fixture.Send(HttpMethod.Post, QuotesUrl(campaign.Id, entry.Id), new { noteId = meNote.Id })).Status.Should().Be(404);
    }

    [Fact]
    public async Task PromoteFollowsEditAccess_AndAnEntryTheCallerCannotSeeIsA404()
    {
        var (campaign, entry) = await NewEntry("Promote permission");
        var note = await Note(campaign.Id, "We met Gundren.");
        (await fixture.PutEntryEditAccess(campaign.Id, entry.Id, EditAccess.OnlyMe)).Should().Succeed();

        fixture.LoginAsUser(Users.Outsider);
        (await fixture.Send(HttpMethod.Post, QuotesUrl(campaign.Id, entry.Id), new { noteId = note.Id })).Status.Should().Be(403);
        await Promote(Users.DM, campaign.Id, entry.Id, note.Id);

        fixture.LoginAsUser(Users.Player);
        var mine = (await fixture.PostEntry(campaign.Id, "My secret", EntryKind.Other, Visibility.Me)).Value;
        fixture.LoginAsUser(Users.Outsider);
        (await fixture.Send(HttpMethod.Post, QuotesUrl(campaign.Id, mine.Id), new { noteId = note.Id })).Status.Should().Be(404);
    }

    public static TheoryData<string, Visibility, Visibility, bool, bool> Audiences => new()
    {
        // note kind, note visibility, quote visibility, DM sees it, other player sees it
        { "everyone", Visibility.Everyone, Visibility.Everyone, true, true },
        { "hidden", Visibility.Everyone, Visibility.DM, true, false },
        { "dm", Visibility.DM, Visibility.DM, true, false },
        { "me", Visibility.Me, Visibility.Me, false, false },
    };

    [Theory]
    [MemberData(nameof(Audiences))]
    public async Task AQuote_GetsItsNotesAudience(string kind, Visibility noteVisibility, Visibility quoteVisibility, bool dmSees, bool otherSees)
    {
        var (campaign, entry) = await NewEntry($"Promote audience {kind}");
        var note = await Note(campaign.Id, "Gundren is Klarg's prisoner.", noteVisibility);
        if (kind == "hidden")
        {
            fixture.LoginAsUser(Users.DM);
            (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed();
        }

        var mark = fixture.Hub.Messages.Count;
        var promoted = await Promote(Users.Player, campaign.Id, entry.Id, note.Id);
        var pushed = fixture.Hub.Messages.Skip(mark).ToList();

        var quote = promoted.Entry.Article.Blocks.Single();
        quote.Visibility.Should().Be(quoteVisibility);
        quote.OwnerMemberId.Should().Be(campaign.PlayerMemberId);
        (await Blocks(Users.DM, campaign.Id, entry.Id)).Any().Should().Be(dmSees);
        (await Blocks(Users.Outsider, campaign.Id, entry.Id)).Any().Should().Be(otherSees);

        var expected = new List<string> { CampaignGroups.Member(campaign.PlayerMemberId) };
        if (dmSees) expected.Add(CampaignGroups.Member(campaign.DmMemberId));
        if (otherSees) expected.Add(CampaignGroups.Member(campaign.SecondPlayerMemberId!.Value));
        var message = pushed.Should().ContainSingle(m => m.Method == CampaignHubMessages.EntryArticleChanged).Subject;
        message.Groups.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task PromotingTheSameTextTwice_IsAllowed()
    {
        var (campaign, entry) = await NewEntry("Promote twice");
        var note = await Note(campaign.Id, "We met Gundren.");

        await Promote(Users.Player, campaign.Id, entry.Id, note.Id, "Gundren");
        var second = await Promote(Users.Player, campaign.Id, entry.Id, note.Id, "Gundren");

        second.Entry.Article.Blocks.Should().HaveCount(2).And.AllSatisfy(b => b.Text.Should().Be("Gundren"));
    }

    [Fact]
    public async Task AQuotesSource_SurvivesAnEdit_ButItsTextCanBeTrimmed()
    {
        var (campaign, entry) = await NewEntry("Promote then trim");
        var note = await Note(campaign.Id, "We met Gundren on the road. He hired us.");
        var promoted = await Promote(Users.Outsider, campaign.Id, entry.Id, note.Id);
        var quote = promoted.Entry.Article.Blocks.Single();

        fixture.LoginAsUser(Users.Outsider);
        var trimmed = await fixture.PutEntryArticle(campaign.Id, entry.Id, promoted.Entry.Article.Etag, [BlockEdit.Change(quote, "He hired us.")]);

        var block = trimmed.Value.Article.Blocks.Single();
        block.Text.Should().Be("He hired us.");
        block.Quote.Should().BeEquivalentTo(quote.Quote);
    }
}
