using System.Text.Json;
using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// Merge (15g.1): the alias, the appended article with its secret blocks kept, the redirect,
/// mentions that resolve without rewriting text, chains, the visibility guard, permissions,
/// claims and the pushes. <see cref="Users.DM"/> is the DM, <see cref="Users.Player"/> and
/// <see cref="Users.Outsider"/> the players.
/// </summary>
public class MergeTests(RecordingHubFixture fixture) : IClassFixture<RecordingHubFixture>
{
    private async Task<EntryResponse> Entry(Users user, Guid campaignId, string name, Visibility visibility = Visibility.Everyone, EntryKind kind = EntryKind.Character)
    {
        fixture.LoginAsUser(user);
        var entry = await fixture.PostEntry(campaignId, name, kind, visibility);
        entry.Should().Succeed();
        return entry.Value;
    }

    private async Task<EntryResponse> Get(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        var entry = await fixture.GetEntry(campaignId, entryId);
        entry.Should().Succeed();
        return entry.Value;
    }

    private async Task<EntryResponse> Merge(Users user, Guid campaignId, Guid fromId, Guid intoId)
    {
        fixture.LoginAsUser(user);
        var merged = await fixture.PostEntryMerge(campaignId, fromId, intoId);
        merged.Should().Succeed();
        return merged.Value;
    }

    private async Task<(int Status, JsonElement Errors)> Refused(Users user, Guid campaignId, Guid fromId, Guid intoId)
    {
        fixture.LoginAsUser(user);
        var (status, body) = await fixture.Send(HttpMethod.Post, EntryUrl(campaignId, fromId, "merge"), new { intoEntryId = intoId });
        using var json = JsonDocument.Parse(body.Length == 0 ? "{}" : body);
        return (status, json.RootElement.TryGetProperty("errors", out var errors) ? errors.Clone() : default);
    }

    private static string Mention(EntryResponse entry, string? text = null) => $"@[{text ?? entry.Name}](entry:{entry.Id})";

    [Fact]
    public async Task Merging_MakesTheNameAnAlias_AppendsTheArticleWithItsSecrets_AndRedirects()
    {
        var campaign = await TestCampaign.Create(fixture, "Merge basics");
        var into = await Entry(Users.Player, campaign.Id, "Gundren Rockseeker");
        (await fixture.PutEntryArticle(campaign.Id, into.Id, into.Article.Etag, [new BlockEdit(null, "Dwarf prospector.")])).Should().Succeed();
        var from = await Entry(Users.Outsider, campaign.Id, "Gundren");
        (await fixture.PutEntryAliases(campaign.Id, from.Id, "The old dwarf")).Should().Succeed();
        from = await Get(Users.DM, campaign.Id, from.Id);
        (await fixture.PutEntryArticle(campaign.Id, from.Id, from.Article.Etag,
            [new BlockEdit(null, "Hired us."), new BlockEdit(null, "Captured by Klarg.", Visibility.DM)])).Should().Succeed();

        var merged = await Merge(Users.Player, campaign.Id, from.Id, into.Id);

        merged.Id.Should().Be(into.Id);
        merged.Aliases.Should().BeEquivalentTo(["Gundren", "The old dwarf"]);
        merged.MergedFromIds.Should().Equal(from.Id);
        // The player sees the heading and the ordinary block, not the DM's secret.
        merged.Article.Blocks.Select(b => b.Text).Should().Equal("Dwarf prospector.", "Merged from Gundren", "Hired us.");
        var dmView = await Get(Users.DM, campaign.Id, into.Id);
        dmView.Article.Blocks.Select(b => b.Text).Should().Equal("Dwarf prospector.", "Merged from Gundren", "Hired us.", "Captured by Klarg.");
        var secret = dmView.Article.Blocks[^1];
        secret.Visibility.Should().Be(Visibility.DM);
        secret.OwnerMemberId.Should().Be(campaign.DmMemberId, "blocks keep their owners");

        // The old id redirects, for reads and writes, and lists leave it out.
        (await Get(Users.Outsider, campaign.Id, from.Id)).Id.Should().Be(into.Id);
        fixture.LoginAsUser(Users.Outsider);
        (await fixture.PutEntryName(campaign.Id, from.Id, "Gundren the Prospector")).Value.Id.Should().Be(into.Id);
        (await fixture.GetEntries(campaign.Id)).Value.Entries.Select(e => e.Entry.Id).Should().Equal(into.Id);

        // Both events carry the actor and share a correlation id.
        var fromEvents = await TestCampaign.EventsOf(fixture, from.Id);
        var intoEvents = await TestCampaign.EventsOf(fixture, into.Id);
        var mergedEvent = fromEvents.Should().ContainSingle(e => e.Data is EntryMerged).Subject;
        var absorbed = intoEvents.Should().ContainSingle(e => e.Data is EntryAbsorbed).Subject;
        ((EntryMerged)mergedEvent.Data).Actor.MemberId.Should().Be(campaign.PlayerMemberId);
        mergedEvent.CorrelationId.Should().NotBeNullOrEmpty().And.Be(absorbed.CorrelationId);
    }

    [Fact]
    public async Task MentionsOfTheMergedEntry_ResolveToTheTarget_InTimelinesAndCounts_WithoutRewritingText()
    {
        var campaign = await TestCampaign.Create(fixture, "Merge mentions");
        var into = await Entry(Users.Player, campaign.Id, "Gundren Rockseeker");
        var from = await Entry(Users.Outsider, campaign.Id, "Gundren");
        var other = await Entry(Users.Player, campaign.Id, "Sildar");

        fixture.LoginAsUser(Users.Player);
        var oldNote = (await fixture.PostSessionNote(campaign.Id, $"We met {Mention(from)}.")).Value;
        var newNote = (await fixture.PostSessionNote(campaign.Id, $"{Mention(into)} hired us.")).Value;
        var both = (await fixture.PostSessionNote(campaign.Id, $"{Mention(from)} is {Mention(into)}.")).Value;
        // Sildar's article mentions the merged entry.
        (await fixture.PutEntryArticle(campaign.Id, other.Id, other.Article.Etag, [new BlockEdit(null, $"Friend of {Mention(from)}.")])).Should().Succeed();

        await Merge(Users.DM, campaign.Id, from.Id, into.Id);

        foreach (var user in new[] { Users.DM, Users.Player, Users.Outsider })
        {
            fixture.LoginAsUser(user);
            var timeline = (await fixture.GetEntryTimeline(campaign.Id, into.Id)).Value;
            timeline.Items.Select(i => i.Note.Id).Should().Equal(oldNote.Id, newNote.Id, both.Id);
            timeline.Items[0].Note.Text.Should().Be($"We met {Mention(from)}.", "the text is never rewritten (invariant 6)");
            timeline.ArticleMentions.Select(m => m.EntryId).Should().Equal(other.Id);
            (await fixture.GetEntryTimeline(campaign.Id, from.Id)).Value.Items.Should().HaveCount(3, "the old id's timeline is the target's");

            var listed = (await fixture.GetEntries(campaign.Id)).Value.Entries.Single(e => e.Entry.Id == into.Id);
            listed.MentionCount.Should().Be(4, "three notes, the one mentioning both counted once, and Sildar's block");
        }
    }

    [Fact]
    public async Task AChainOfMerges_ResolvesInOneStep()
    {
        var campaign = await TestCampaign.Create(fixture, "Merge chain");
        var a = await Entry(Users.Player, campaign.Id, "Gundren");
        var b = await Entry(Users.Player, campaign.Id, "Rockseeker");
        var c = await Entry(Users.Player, campaign.Id, "Gundren Rockseeker");
        fixture.LoginAsUser(Users.Player);
        var note = (await fixture.PostSessionNote(campaign.Id, $"We met {Mention(a)}.")).Value;

        await Merge(Users.Player, campaign.Id, a.Id, b.Id);
        var merged = await Merge(Users.Player, campaign.Id, b.Id, c.Id);

        merged.MergedFromIds.Should().BeEquivalentTo([a.Id, b.Id]);
        merged.Aliases.Should().BeEquivalentTo(["Gundren", "Rockseeker"]);
        merged.Article.Blocks.Select(x => x.Text).Should().Equal("Merged from Rockseeker", "Merged from Gundren");
        (await Get(Users.Outsider, campaign.Id, a.Id)).Id.Should().Be(c.Id);
        fixture.LoginAsUser(Users.Outsider);
        (await fixture.GetEntryTimeline(campaign.Id, c.Id)).Value.Items.Select(i => i.Note.Id).Should().Equal(note.Id);
        (await fixture.GetEntries(campaign.Id)).Value.Entries.Single().MentionCount.Should().Be(1);
    }

    [Fact]
    public async Task TheVisibilityGuard_RefusesAMergeThatWouldReveal_AndAllowsOneThatNarrows()
    {
        var campaign = await TestCampaign.Create(fixture, "Merge guard");
        var glasstaff = await Entry(Users.DM, campaign.Id, "Glasstaff", Visibility.DM);
        var iarno = await Entry(Users.DM, campaign.Id, "Iarno");
        var mine = await Entry(Users.Player, campaign.Id, "My secret", Visibility.Me);
        var playersDm = await Entry(Users.Player, campaign.Id, "Player's DM note", Visibility.DM);

        // A DM entry into an Everyone one would show it to the players.
        var (status, errors) = await Refused(Users.DM, campaign.Id, glasstaff.Id, iarno.Id);
        status.Should().Be(409);
        errors.GetProperty(PostEntryMerge.VisibilityErrorKey)[0].GetString().Should().Contain("Change visibility first");
        // A Me entry into anything someone else sees, too.
        (await Refused(Users.Player, campaign.Id, mine.Id, iarno.Id)).Status.Should().Be(409);
        // The DM cannot see the player's Me entry at all.
        (await Refused(Users.DM, campaign.Id, glasstaff.Id, mine.Id)).Status.Should().Be(404);

        // Everyone into DM narrows: allowed, and the players lose it.
        var mark = fixture.Hub.Messages.Count;
        var merged = await Merge(Users.DM, campaign.Id, iarno.Id, glasstaff.Id);
        merged.Aliases.Should().Equal("Iarno");
        var pushed = fixture.Hub.Messages.Skip(mark).ToList();
        pushed.Select(m => m.Method).Should().Equal(
            CampaignHubMessages.EntryRemoved, CampaignHubMessages.EntryMerged, CampaignHubMessages.EntryUpserted);
        pushed[0].Groups.Should().Equal(CampaignGroups.Campaign(campaign.Id));
        ((EntryRemovedMessage)pushed[0].Payload!).EntryId.Should().Be(iarno.Id);
        pushed[1].Groups.Should().BeEquivalentTo([CampaignGroups.Dms(campaign.Id), CampaignGroups.Member(campaign.DmMemberId)]);
        pushed[1].Payload.Should().Be(new EntryMergedMessage(iarno.Id, glasstaff.Id));
        fixture.LoginAsUser(Users.Outsider);
        await fixture.ExpectStatus(HttpMethod.Get, EntryUrl(campaign.Id, iarno.Id), null, 404);

        // Two DM entries: the player's (the DMs and that player) and the DM's (the DMs). The
        // DM's into the player's would show it to the player; the player's into the DM's hides
        // it from them, which is allowed.
        var dmOwn = await Entry(Users.DM, campaign.Id, "DM's DM note", Visibility.DM);
        (await Refused(Users.DM, campaign.Id, dmOwn.Id, playersDm.Id)).Status.Should().Be(409);
        (await Merge(Users.DM, campaign.Id, playersDm.Id, dmOwn.Id)).Id.Should().Be(dmOwn.Id);
    }

    [Fact]
    public async Task TheCaller_MustBeAbleToEditBoth_AndNeitherMayBeMerged()
    {
        var campaign = await TestCampaign.Create(fixture, "Merge permissions");
        var locked = await Entry(Users.Player, campaign.Id, "Gundren Rockseeker");
        (await fixture.PutEntryEditAccess(campaign.Id, locked.Id, EditAccess.OnlyMe)).Should().Succeed();
        var open = await Entry(Users.Outsider, campaign.Id, "Gundren");
        var hidden = await Entry(Users.Player, campaign.Id, "Hidden", Visibility.Me);

        (await Refused(Users.Outsider, campaign.Id, open.Id, locked.Id)).Status.Should().Be(403);
        (await Refused(Users.Outsider, campaign.Id, locked.Id, open.Id)).Status.Should().Be(403);
        (await Refused(Users.Outsider, campaign.Id, open.Id, hidden.Id)).Status.Should().Be(404);
        (await Refused(Users.Outsider, campaign.Id, open.Id, open.Id)).Status.Should().Be(400);

        await Merge(Users.DM, campaign.Id, open.Id, locked.Id);
        var again = await Refused(Users.DM, campaign.Id, open.Id, locked.Id);
        again.Status.Should().Be(409);
        again.Errors.GetProperty(PostEntryMerge.MergedErrorKey)[0].GetString().Should().Contain("already been merged");
        var third = await Entry(Users.DM, campaign.Id, "Third");
        (await Refused(Users.DM, campaign.Id, third.Id, open.Id)).Status.Should().Be(409, "the target is merged");
    }

    [Fact]
    public async Task AClaimedEntry_MergesOnlyIntoAnUnclaimedOrSameClaimerCharacter_AndTheClaimMoves()
    {
        var campaign = await TestCampaign.Create(fixture, "Merge claims");
        var pc = await Entry(Users.Player, campaign.Id, "Tordek");
        (await fixture.PutEntryClaim(campaign.Id, pc.Id, campaign.PlayerMemberId)).Should().Succeed();
        var place = await Entry(Users.Player, campaign.Id, "Phandalin", kind: EntryKind.Place);
        var othersPc = await Entry(Users.Outsider, campaign.Id, "Lidda");
        (await fixture.PutEntryClaim(campaign.Id, othersPc.Id, campaign.SecondPlayerMemberId)).Should().Succeed();
        var npc = await Entry(Users.Player, campaign.Id, "Tordek the Dwarf");

        var (status, errors) = await Refused(Users.DM, campaign.Id, pc.Id, place.Id);
        status.Should().Be(409);
        errors.GetProperty(PostEntryMerge.ClaimErrorKey)[0].GetString().Should().Contain("Character");
        (await Refused(Users.DM, campaign.Id, pc.Id, othersPc.Id)).Status.Should().Be(409);

        var merged = await Merge(Users.Player, campaign.Id, pc.Id, npc.Id);
        merged.ClaimedByMemberId.Should().Be(campaign.PlayerMemberId);
    }

    [Fact]
    public async Task TooManyAliases_IsRefused()
    {
        var campaign = await TestCampaign.Create(fixture, "Merge aliases");
        var into = await Entry(Users.Player, campaign.Id, "Gundren Rockseeker");
        (await fixture.PutEntryAliases(campaign.Id, into.Id, Enumerable.Range(1, 15).Select(i => $"Into {i}").ToArray())).Should().Succeed();
        var from = await Entry(Users.Player, campaign.Id, "Gundren");
        (await fixture.PutEntryAliases(campaign.Id, from.Id, Enumerable.Range(1, 5).Select(i => $"From {i}").ToArray())).Should().Succeed();

        var (status, errors) = await Refused(Users.Player, campaign.Id, from.Id, into.Id);
        status.Should().Be(409);
        errors.GetProperty(PostEntryMerge.AliasesErrorKey)[0].GetString().Should().Contain("21 aliases");
    }
}
