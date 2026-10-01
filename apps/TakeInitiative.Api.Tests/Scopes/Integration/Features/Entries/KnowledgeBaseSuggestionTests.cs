using FluentAssertions;

using Npgsql;

using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Reference;
using TakeInitiative.Api.Tests.Integration.Features.Reference;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using TakeInitiative.KnowledgeBase.Schema;

using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// <c>GET …/knowledge-base-suggestions</c> and <c>POST …/dismiss</c> (28b): what counts as a match,
/// what never does, who is offered one, and what a dismissal silences.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two rungs are the subject.</b> The fixture corpus has one "Test Gremlin" and three names
/// that start with it, so one entry exercises the exact rung, the prefix rung, the order between them
/// and the cap at once. "Test Gremlan" is the fuzzy near-miss: ⌘K's browse endpoint finds it, which
/// is asserted in the same test, and the prompt does not.
/// </para>
/// <para>
/// <b>The empty answers are the point of the suite.</b> A player on an unclaimed Character and a
/// member who may not edit both get <c>[]</c> with a 200, never a 403 — a 403 would say "there is
/// something here you may not see", which is the leak with a status code in front of it. The tests
/// assert the status code explicitly rather than only the body.
/// </para>
/// </remarks>
public class KnowledgeBaseSuggestionTests(KnowledgeBaseFixture fixture) : IClassFixture<KnowledgeBaseFixture>
{
    private const string Provider = KnowledgeBaseCorpus.Provider;
    private const string Gremlin = "monster_test-gremlin_tst";
    private const string Chief = "monster_test-gremlin-chief_tst";
    private const string Zombie = "monster_test-gremlin-zombie_tsta";
    private const string Understudy = "monster_test-gremlin-understudy_tst";
    private const string Mossback = "monster_test-mossback_tst";
    private const string Pike = "item_test-pike_tst";
    private const string Sparkburst = "spell_test-sparkburst_tst";
    private const string Glimmer = "spell_test-glimmer_tst";

    private async Task<EntryResponse> Entry(Users user, Guid campaignId, string name, EntryKind kind)
    {
        fixture.LoginAsUser(user);
        var entry = await fixture.PostEntry(campaignId, name, kind);
        entry.Should().Succeed();
        return entry.Value;
    }

    /// <summary>The ids suggested to <paramref name="user" />, in the order they were offered.</summary>
    private async Task<string[]> Suggested(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        var response = await fixture.GetKnowledgeBaseSuggestions(campaignId, entryId);
        response.Should().Succeed();
        return [.. response.Value.Items.Select(item => item.Id)];
    }

    [Fact]
    public async Task AnExactMatchComesFirst_ThenThePrefixes_ShorterNameFirst_CappedAtThree()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: exact");
        var gremlin = await Entry(Users.DM, campaign.Id, "Test Gremlin", EntryKind.Character);

        var suggested = await Suggested(Users.DM, campaign.Id, gremlin.Id);

        // Four rows qualify — the exact one and three names it is a prefix of — and the prompt shows
        // the best three: rung first, then the shorter name.
        suggested.Should().Equal([Gremlin, Chief, Zombie]);
        suggested.Should().NotContain(Understudy, "a prompt offers at most three candidates");
    }

    [Fact]
    public async Task TheResponseCarriesWhatTheRowSays_SoTheMemberCanJudge_AndNoScore()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: row");
        var gremlin = await Entry(Users.DM, campaign.Id, "Test Gremlin", EntryKind.Character);

        fixture.LoginAsUser(Users.DM);
        var response = await fixture.GetKnowledgeBaseSuggestions(campaign.Id, gremlin.Id);
        response.Should().Succeed();

        var row = response.Value.Items.First();
        row.Provider.Should().Be(Provider);
        row.ProviderLabel.Should().Be("5eTools");
        row.Id.Should().Be(Gremlin);
        row.Name.Should().Be("Test Gremlin");
        row.Category.Should().Be(ReferenceCategory.Monster);
        row.Label.Should().Be("CR 1/2 · Small Fey", "the row's own detail line is what lets the member judge");
        row.Book.Should().Be("TST");
        row.BookTitle.Should().Be("Test Book of Beasts");
        row.Url.Should().Be("https://5e.tools/bestiary.html#test%20gremlin_tst");
    }

    [Fact]
    public async Task APrefixMatchIsOffered_WithNoExactRowAtAll()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: prefix");
        var grem = await Entry(Users.DM, campaign.Id, "Test Grem", EntryKind.Character);

        (await Suggested(Users.DM, campaign.Id, grem.Id)).Should().Equal([Gremlin, Chief, Zombie]);
    }

    [Fact]
    public async Task TheMatchFoldsCase()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: folding");
        var shouty = await Entry(Users.DM, campaign.Id, "TEST GREMLIN", EntryKind.Character);

        (await Suggested(Users.DM, campaign.Id, shouty.Id)).Should().StartWith([Gremlin]);
    }

    [Fact]
    public async Task AFuzzyNearMiss_IsNothingAtAll_ThoughSearchStillFindsIt()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: fuzzy");
        var typo = await Entry(Users.DM, campaign.Id, "Test Gremlan", EntryKind.Character);

        (await Suggested(Users.DM, campaign.Id, typo.Id)).Should().BeEmpty();

        // The same text, asked for: ⌘K's ladder reaches rung 4 and answers the gremlins. That is the
        // difference the prompt exists to keep — a search was asked for, and a prompt was not.
        fixture.LoginAsUser(Users.DM);
        var searched = await fixture.GetKnowledgeBase(campaign.Id, q: "Test Gremlan");
        searched.Items.Should().Contain(item => item.Id == Gremlin, "a fuzzy rung is fine when the member typed the query");
    }

    [Theory]
    [InlineData(EntryKind.Place)]
    [InlineData(EntryKind.Faction)]
    [InlineData(EntryKind.Event)]
    public async Task AnIncompatibleKindIsNeverOffered_WhateverItIsCalled(EntryKind kind)
    {
        var campaign = await TestCampaign.Create(fixture, $"Suggest: kind {kind}");
        var named = await Entry(Users.DM, campaign.Id, "Test Gremlin", kind);

        (await Suggested(Users.DM, campaign.Id, named.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task AnItemMayBeAnItemOrASpell_AndAnOtherMayBeASpell()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: categories");
        var pike = await Entry(Users.DM, campaign.Id, "Test Pike", EntryKind.Item);
        var sparkburst = await Entry(Users.DM, campaign.Id, "Test Sparkburst", EntryKind.Item);
        var glimmer = await Entry(Users.DM, campaign.Id, "Test Glimmer", EntryKind.Other);
        // A monster is a Character's match and nobody else's: an Item named after one is not it.
        var gremlin = await Entry(Users.DM, campaign.Id, "Test Gremlin", EntryKind.Item);

        (await Suggested(Users.DM, campaign.Id, pike.Id)).Should().Equal([Pike]);
        (await Suggested(Users.DM, campaign.Id, sparkburst.Id)).Should().Equal([Sparkburst]);
        // Spell rows have no entry kind of their own — + Wiki files them under Other — so Other has
        // to match them or a spell could never be suggested at all.
        (await Suggested(Users.DM, campaign.Id, glimmer.Id)).Should().Equal([Glimmer]);
        (await Suggested(Users.DM, campaign.Id, gremlin.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task ARowTheEntryAlreadyLinks_IsNotOfferedAgain()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: linked");
        var gremlin = await Entry(Users.DM, campaign.Id, "Test Gremlin", EntryKind.Character);

        fixture.LoginAsUser(Users.DM);
        (await fixture.PostKnowledgeBaseLink(campaign.Id, gremlin.Id, Provider, Gremlin)).Should().Succeed();

        var suggested = await Suggested(Users.DM, campaign.Id, gremlin.Id);
        suggested.Should().NotContain(Gremlin, "it is linked: there is nothing left to ask");
        suggested.Should().Equal([Chief, Zombie, Understudy], "the other candidates move up");
    }

    [Fact]
    public async Task AStaleRowIsNotOffered()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: stale");
        // A staled row is one a prune kept only because an entry links it (26c): unreachable by
        // search and by browse, so the prompt must not reach it either — a member who tapped
        // "Link it" would get a link that reads "no longer in your knowledge base" at once. The
        // column is set directly here: how the row was staled is not what is under test, and
        // EntryLinkPruneTests already covers the prune that sets it.
        await Stale(Mossback, true);
        try
        {
            var mossback = await Entry(Users.DM, campaign.Id, "Test Mossback", EntryKind.Character);

            (await Suggested(Users.DM, campaign.Id, mossback.Id)).Should().BeEmpty();

            // And it is offered again once it is back in the corpus, so the exclusion is the flag
            // and not the row.
            await Stale(Mossback, false);
            (await Suggested(Users.DM, campaign.Id, mossback.Id)).Should().Equal([Mossback]);
        }
        finally
        {
            await Stale(Mossback, false);
        }
    }

    [Fact]
    public async Task ADismissalSilencesOneCandidateAndNotAnother_IsIdempotent_AndIsCampaignWide()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: dismiss");
        var secondDm = campaign.SecondPlayerMemberId!.Value;
        await campaign.PromoteToDm(fixture, secondDm);
        var gremlin = await Entry(Users.DM, campaign.Id, "Test Gremlin", EntryKind.Character);

        fixture.LoginAsUser(Users.DM);
        var left = await fixture.PostDismissSuggestion(campaign.Id, gremlin.Id, Provider, Gremlin);
        left.Should().Succeed();

        // Per candidate row, not per entry: dismissing the exact one leaves the three prefixes.
        left.Value.Items.Select(i => i.Id).Should().Equal([Chief, Zombie, Understudy]);
        (await Suggested(Users.DM, campaign.Id, gremlin.Id)).Should().Equal([Chief, Zombie, Understudy]);

        // Idempotent: the same dismissal again is a 200 and appends nothing.
        fixture.LoginAsUser(Users.DM);
        (await fixture.PostDismissSuggestion(campaign.Id, gremlin.Id, Provider, Gremlin)).Should().Succeed();
        var events = await TestCampaign.EventsOf(fixture, gremlin.Id);
        events.Select(e => e.Data).OfType<EntryKnowledgeBaseSuggestionDismissed>().Should().HaveCount(1);

        // Campaign-wide: the other DM is not asked the question again either.
        (await Suggested(Users.Outsider, campaign.Id, gremlin.Id)).Should().Equal([Chief, Zombie, Understudy]);

        // And a dismissal of a row this entry never matched changes nothing it did match.
        fixture.LoginAsUser(Users.DM);
        (await fixture.PostDismissSuggestion(campaign.Id, gremlin.Id, Provider, Mossback)).Should().Succeed();
        (await Suggested(Users.DM, campaign.Id, gremlin.Id)).Should().Equal([Chief, Zombie, Understudy]);
    }

    [Fact]
    public async Task ADismissalSpelledWithAnotherProviderCase_IsTheSameRow()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: dismiss case");
        var gremlin = await Entry(Users.DM, campaign.Id, "Test Gremlin", EntryKind.Character);

        fixture.LoginAsUser(Users.DM);
        // The provider's own spelling is "5etools"; a caller shouting it names the same row, and the
        // stored dismissal takes the provider's spelling so a later link to it still matches.
        (await fixture.PostDismissSuggestion(campaign.Id, gremlin.Id, "5eTOOLS", Gremlin)).Should().Succeed();

        (await Suggested(Users.DM, campaign.Id, gremlin.Id)).Should().NotContain(Gremlin);
        var events = await TestCampaign.EventsOf(fixture, gremlin.Id);
        events.Select(e => e.Data).OfType<EntryKnowledgeBaseSuggestionDismissed>()
            .Should().ContainSingle().Which.Provider.Should().Be(Provider);
    }

    [Fact]
    public async Task AnAliasIsMatchedAsWellAsTheName()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: alias");
        var eye = await Entry(Users.DM, campaign.Id, "The Eye", EntryKind.Character);

        (await Suggested(Users.DM, campaign.Id, eye.Id)).Should().BeEmpty("nothing is called The Eye");

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryAliases(campaign.Id, eye.Id, "Test Gremlin")).Should().Succeed();

        (await Suggested(Users.DM, campaign.Id, eye.Id)).Should().Equal([Gremlin, Chief, Zombie]);
    }

    [Fact]
    public async Task APlayerOnAnUnclaimedCharacter_GetsAnEmptyListAndA200_AndTheClaimerGetsThePrompt()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: npc");
        var gremlin = await Entry(Users.DM, campaign.Id, "Test Gremlin", EntryKind.Character);

        (await Suggested(Users.DM, campaign.Id, gremlin.Id)).Should().Contain(Gremlin);

        // A prompt naming the monster is the stat-block leak the links read rule exists to stop, and
        // it is answered with an empty list rather than a 403: a 403 would say there is something
        // here (invariant 5 applies to status codes).
        fixture.LoginAsUser(Users.Player);
        var (status, body) = await fixture.Send(HttpMethod.Get, SuggestionsUrl(campaign.Id, gremlin.Id));
        status.Should().Be(200, body);
        body.Should().NotContain("Test Gremlin").And.NotContain(Gremlin);
        (await Suggested(Users.Player, campaign.Id, gremlin.Id)).Should().BeEmpty();

        // Claimed, it is that player's character: they may read its links and write them, so they are
        // exactly who the prompt is for.
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryClaim(campaign.Id, gremlin.Id, campaign.PlayerMemberId)).Should().Succeed();
        (await Suggested(Users.Player, campaign.Id, gremlin.Id)).Should().Contain(Gremlin);
    }

    [Fact]
    public async Task AMemberWhoMayNotEditGetsAnEmptyList_AndCannotDismiss()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: reader");
        var pike = await Entry(Users.DM, campaign.Id, "Test Pike", EntryKind.Item);
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryEditAccess(campaign.Id, pike.Id, EditAccess.OnlyMe)).Should().Succeed();

        // An Item's links are read by everyone who sees it, so this is the write half of the rule on
        // its own: a question whose only available answer is "dismiss" is noise.
        fixture.LoginAsUser(Users.Player);
        var (status, body) = await fixture.Send(HttpMethod.Get, SuggestionsUrl(campaign.Id, pike.Id));
        status.Should().Be(200, body);
        body.Should().NotContain(Pike);

        (await Suggested(Users.DM, campaign.Id, pike.Id)).Should().Equal([Pike]);

        // Dismissing is the write path, so it is a 403 there: nothing is disclosed by refusing a
        // write the caller named itself.
        fixture.LoginAsUser(Users.Player);
        (await fixture.Send(HttpMethod.Post, DismissSuggestionUrl(campaign.Id, pike.Id), DismissSuggestionBody(Provider, Pike)))
            .Status.Should().Be(403);
    }

    [Fact]
    public async Task AnEntryTheCallerCannotSee_IsA404_FromBothEndpoints()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: 404");
        fixture.LoginAsUser(Users.DM);
        var secret = await fixture.PostEntry(campaign.Id, "Test Gremlin", EntryKind.Character, Visibility.Me);
        secret.Should().Succeed();

        // The entry's own read rule still decides, as everywhere else: an entry the caller cannot see
        // is not there, which is a 404 and not an empty list.
        fixture.LoginAsUser(Users.Player);
        (await fixture.Send(HttpMethod.Get, SuggestionsUrl(campaign.Id, secret.Value.Id))).Status.Should().Be(404);
        (await fixture.Send(HttpMethod.Post, DismissSuggestionUrl(campaign.Id, secret.Value.Id), DismissSuggestionBody(Provider, Gremlin)))
            .Status.Should().Be(404);
    }

    [Theory]
    [InlineData("", Gremlin)]
    [InlineData("   ", Gremlin)]
    [InlineData(Provider, "")]
    [InlineData(null, null)]
    public async Task ADismissalMissingEitherHalfOfItsKey_IsA400(string? provider, string? itemId)
    {
        var campaign = await TestCampaign.Create(fixture, $"Suggest: bad key {provider}/{itemId}");
        var gremlin = await Entry(Users.DM, campaign.Id, "Test Gremlin", EntryKind.Character);

        fixture.LoginAsUser(Users.DM);
        (await fixture.Send(HttpMethod.Post, DismissSuggestionUrl(campaign.Id, gremlin.Id), DismissSuggestionBody(provider, itemId)))
            .Status.Should().Be(400);
    }

    [Fact]
    public async Task ADismissalOfARowThatIsNotThere_IsAccepted()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: unknown row");
        var gremlin = await Entry(Users.DM, campaign.Id, "Test Gremlin", EntryKind.Character);

        // No check that the provider is registered or the row is still in the corpus: a dismissal is
        // a statement about this entry, and refusing one for a row that has since been pruned would
        // leave the member unable to silence a prompt they can see.
        fixture.LoginAsUser(Users.DM);
        var left = await fixture.PostDismissSuggestion(campaign.Id, gremlin.Id, "no-such-provider", "no-such-row");
        left.Should().Succeed();
        left.Value.Items.Select(i => i.Id).Should().Equal([Gremlin, Chief, Zombie]);
    }

    [Fact]
    public async Task HistoryShowsTheDismissalAndTheLink_AndOnlyToWhoMayReadTheLinks()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest: history");
        var gremlin = await Entry(Users.DM, campaign.Id, "Test Gremlin", EntryKind.Character);

        fixture.LoginAsUser(Users.DM);
        (await fixture.PostDismissSuggestion(campaign.Id, gremlin.Id, Provider, Chief)).Should().Succeed();
        (await fixture.PostKnowledgeBaseLink(campaign.Id, gremlin.Id, Provider, Gremlin)).Should().Succeed();

        fixture.LoginAsUser(Users.DM);
        var dm = (await fixture.GetEntryHistory(campaign.Id, gremlin.Id)).Value.Items;
        var dismissed = dm.Should().ContainSingle(i => i.Change.Type == EntryChangeType.SuggestionDismissed).Subject;
        dismissed.ActorMemberId.Should().Be(campaign.DmMemberId);
        dismissed.Change.Suggestion!.Name.Should().Be("Test Gremlin Chief", "the row is named, not its id");
        dismissed.Change.Suggestion!.ProviderLabel.Should().Be("5eTools");
        dismissed.Change.Suggestion!.ItemId.Should().Be(Chief);
        dismissed.Change.Suggestion!.Detail.Should().Be("CR 2 · Small Fey · TST");
        dm.Should().ContainSingle(i => i.Change.Type == EntryChangeType.LinkAdded);

        // On an unclaimed Character a dismissal is the same disclosure a link is — it names the
        // monster — so the player's history has neither row, and the bytes hold neither name.
        fixture.LoginAsUser(Users.Player);
        var (status, body) = await fixture.Send(HttpMethod.Get, EntryUrl(campaign.Id, gremlin.Id, "history"));
        status.Should().Be(200, body);
        body.Should().NotContain("Test Gremlin Chief").And.NotContain(Chief);
        var player = (await fixture.GetEntryHistory(campaign.Id, gremlin.Id)).Value.Items;
        player.Should().NotContain(i => i.Change.Type == EntryChangeType.SuggestionDismissed);
        player.Should().NotContain(i => i.Change.Type == EntryChangeType.LinkAdded);
    }

    /// <summary>Sets or clears one row's <c>stale</c> flag, the state a prune leaves behind (26c).</summary>
    private async Task Stale(string itemId, bool stale)
    {
        await using var connection = new NpgsqlConnection(fixture.PostgreSqlContainer.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"update {KnowledgeBaseSchema.Qualified()} set stale = @stale where provider = @provider and id = @id";
        command.Parameters.AddWithValue("stale", stale);
        command.Parameters.AddWithValue("provider", Provider);
        command.Parameters.AddWithValue("id", itemId);
        (await command.ExecuteNonQueryAsync()).Should().Be(1);
    }
}
