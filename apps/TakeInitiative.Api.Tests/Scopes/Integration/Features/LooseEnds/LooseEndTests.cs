using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.LooseEnds;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Connections.ConnectionTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.LooseEnds.LooseEndTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.LooseEnds;

/// <summary>
/// Loose ends (19b.1, design §5): each kind appears to the member who can resolve it and clears
/// when it is resolved by an ordinary edit, because nothing is stored.
/// </summary>
public class LooseEndTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    [Fact]
    public async Task AnUnlinkedNote_IsItsAuthorsLooseEnd_UntilItMentionsAnEntry()
    {
        var campaign = await TestCampaign.Create(fixture, "Loose unlinked", withSecondPlayer: false);
        var gundren = await fixture.Entry(Users.DM, campaign.Id, "Gundren");
        var note = await fixture.Note(Users.Player, campaign.Id, "we met gundren on the road");

        var items = await fixture.LooseEndsOf(Users.Player, campaign.Id);
        items.Keys().Should().Equal((LooseEndKind.UnlinkedNote, note.Id));
        var item = items[0];
        (item.SessionId, item.SessionNumber, item.MentionCount, item.Entry).Should().Be((note.SessionId, 1, (int?)null, (EntrySummaryResponse?)null));
        item.Note!.Text.Should().Be("we met gundren on the road");

        (await fixture.LooseEndsOf(Users.DM, campaign.Id)).Should().NotContain(i => i.Note != null, "a note is its author's to link");

        fixture.LoginAsUser(Users.Player);
        (await fixture.PutSessionNote(campaign.Id, note.Id, $"we met {Mention(gundren)} on the road")).Should().Succeed();
        (await fixture.LooseEndsOf(Users.Player, campaign.Id)).Should().NotContain(i => i.Note != null);
    }

    [Fact]
    public async Task AnUntaggedImageNote_IsItsAuthorsLooseEnd_UntilItsCaptionMentionsAnEntry()
    {
        var campaign = await TestCampaign.Create(fixture, "Loose image", withSecondPlayer: false);
        var cave = await fixture.Entry(Users.DM, campaign.Id, "Cragmaw Hideout", kind: EntryKind.Place);
        var bare = await fixture.ImageNote(Users.Player, campaign.Id);
        var captioned = await fixture.ImageNote(Users.Player, campaign.Id, "the Cragmaw cave");

        (await fixture.LooseEndsOf(Users.Player, campaign.Id)).Keys().Should().Equal(
            (LooseEndKind.UntaggedImageNote, captioned.Id), (LooseEndKind.UntaggedImageNote, bare.Id));

        fixture.LoginAsUser(Users.Player);
        (await fixture.PutImageNote(campaign.Id, captioned.Id, $"the {Mention(cave)}", null)).Should().Succeed();
        var items = await fixture.LooseEndsOf(Users.Player, campaign.Id);
        items.Where(i => i.Note != null).Keys().Should().Equal((LooseEndKind.UntaggedImageNote, bare.Id));
        items.Keys().Should().Contain((LooseEndKind.EmptyArticle, cave.Id), "the player can edit the cave they now mention, and it has no article");
    }

    [Fact]
    public async Task AHiddenNote_IsStillItsAuthorsLooseEnd_ADeletedOneIsNone()
    {
        var campaign = await TestCampaign.Create(fixture, "Loose hidden", withSecondPlayer: false);
        var hidden = await fixture.Note(Users.Player, campaign.Id, "Pizza's here");
        var deleted = await fixture.Note(Users.Player, campaign.Id, "Oops");

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionNoteHidden(campaign.Id, hidden.Id, true)).Should().Succeed();
        fixture.LoginAsUser(Users.Player);
        (await fixture.DeleteSessionNote(campaign.Id, deleted.Id)).Should().Succeed();

        var items = await fixture.LooseEndsOf(Users.Player, campaign.Id);
        items.Keys().Should().Equal((LooseEndKind.UnlinkedNote, hidden.Id));
        items[0].Note!.IsHidden.Should().BeTrue();
        (await fixture.LooseEndsOf(Users.DM, campaign.Id)).Should().BeEmpty("the DM sees the hidden note, but it is not theirs to link");
    }

    [Fact]
    public async Task AnEntryOfKindOther_IsALooseEnd_UntilItsKindIsSet()
    {
        var campaign = await TestCampaign.Create(fixture, "Loose other", withSecondPlayer: false);
        var glasstaff = await fixture.Entry(Users.DM, campaign.Id, "Glasstaff", kind: EntryKind.Other);

        var items = await fixture.LooseEndsOf(Users.DM, campaign.Id);
        items.Keys().Should().Equal((LooseEndKind.OtherKind, glasstaff.Id));
        (items[0].SessionId, items[0].SessionNumber, items[0].MentionCount, items[0].Suggestions.Length).Should().Be(((Guid?)null, (int?)null, (int?)0, 0));

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryKind(campaign.Id, glasstaff.Id, EntryKind.Character)).Should().Succeed();
        (await fixture.LooseEndsOf(Users.DM, campaign.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task AMentionedEntryWithAnEmptyArticle_IsALooseEnd_UntilItIsWritten()
    {
        var campaign = await TestCampaign.Create(fixture, "Loose empty", withSecondPlayer: false);
        var tharden = await fixture.Entry(Users.DM, campaign.Id, "Tharden");
        var phandalin = await fixture.Entry(Users.DM, campaign.Id, "Phandalin", kind: EntryKind.Place);
        var quiet = await fixture.Entry(Users.DM, campaign.Id, "Sildar");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(tharden)} rode to {Mention(phandalin)}.");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(phandalin)} is quiet.");
        await fixture.Article(Users.DM, campaign.Id, quiet.Id, new BlockEdit(null, "   "));

        var items = await fixture.LooseEndsOf(Users.DM, campaign.Id);
        items.Keys().Should().Equal((LooseEndKind.EmptyArticle, phandalin.Id), (LooseEndKind.EmptyArticle, tharden.Id));
        items.Select(i => i.MentionCount).Should().Equal(2, 1);
        items.Should().NotContain(i => i.Entry!.Id == quiet.Id, "an entry nobody mentions is not a loose end, however empty");

        await fixture.Article(Users.DM, campaign.Id, phandalin.Id, new BlockEdit(null, "A frontier town."));
        (await fixture.LooseEndsOf(Users.DM, campaign.Id)).Keys().Should().Equal((LooseEndKind.EmptyArticle, tharden.Id));
    }

    [Fact]
    public async Task AnEntryBothOtherAndEmpty_IsTwoLooseEnds_AndResolvingOneLeavesTheOther()
    {
        var campaign = await TestCampaign.Create(fixture, "Loose both", withSecondPlayer: false);
        var glasstaff = await fixture.Entry(Users.DM, campaign.Id, "Glasstaff", kind: EntryKind.Other);
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(glasstaff)} fled.");

        (await fixture.LooseEndsOf(Users.DM, campaign.Id)).Keys().Should().Equal(
            (LooseEndKind.OtherKind, glasstaff.Id), (LooseEndKind.EmptyArticle, glasstaff.Id));

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryKind(campaign.Id, glasstaff.Id, EntryKind.Character)).Should().Succeed();
        (await fixture.LooseEndsOf(Users.DM, campaign.Id)).Keys().Should().Equal((LooseEndKind.EmptyArticle, glasstaff.Id));
    }

    [Fact]
    public async Task AnEntrysSession_IsItsCreatingNotes_UntilThatNoteIsDeleted()
    {
        var campaign = await TestCampaign.Create(fixture, "Loose created from", withSecondPlayer: false);
        fixture.LoginAsUser(Users.DM);
        var session2 = (await fixture.PostStartSession(campaign.Id, 2)).Value;

        var klarg = new NewEntry(Guid.NewGuid(), "Klarg", EntryKind.Other);
        fixture.LoginAsUser(Users.Player);
        var note = (await fixture.PostSessionNote(campaign.Id, $"{klarg.Mention} roars.", newEntries: klarg)).Value;
        note.SessionId.Should().Be(session2.Id);
        var loose = await fixture.Note(Users.Player, campaign.Id, "Nothing to see");

        var items = await fixture.LooseEndsOf(Users.Player, campaign.Id);
        items.Keys().Should().Equal(
            (LooseEndKind.UnlinkedNote, loose.Id), (LooseEndKind.OtherKind, klarg.Id), (LooseEndKind.EmptyArticle, klarg.Id));
        items.Should().OnlyContain(i => i.SessionId == session2.Id && i.SessionNumber == 2);

        // One divider's loose ends; and none in a session that has none.
        (await fixture.LooseEndsOf(Users.Player, campaign.Id, session2.Id)).Should().HaveCount(3);
        (await fixture.LooseEndsOf(Users.Player, campaign.Id, Guid.NewGuid())).Should().BeEmpty();

        fixture.LoginAsUser(Users.Player);
        (await fixture.DeleteSessionNote(campaign.Id, note.Id)).Should().Succeed();
        items = await fixture.LooseEndsOf(Users.Player, campaign.Id);
        items.Keys().Should().Equal((LooseEndKind.UnlinkedNote, loose.Id), (LooseEndKind.OtherKind, klarg.Id));
        items[1].SessionId.Should().BeNull("its note is gone, so it counts in the Wiki only");
    }
}
