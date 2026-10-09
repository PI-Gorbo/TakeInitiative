using System.Text.Json;

using FluentAssertions;

using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Reference;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// <c>POST links</c> and <c>DELETE links/{id}</c> (27c): every validation rule, the cap, both
/// duplicate cases, who may write, and who the push reaches.
/// </summary>
public class LinkApiTests(KnowledgeBaseFixture fixture) : IClassFixture<KnowledgeBaseFixture>
{
    private const string Gremlin = "monster_test-gremlin_tst";
    private const string Chief = "monster_test-gremlin-chief_tst";
    private const string Provider = KnowledgeBaseCorpus.Provider;

    private async Task<EntryResponse> Entry(Users user, Guid campaignId, string name, EntryKind kind = EntryKind.Place)
    {
        fixture.LoginAsUser(user);
        var entry = await fixture.PostEntry(campaignId, name, kind);
        entry.Should().Succeed();
        return entry.Value;
    }

    private async Task<EntryResponse> Post(Users user, Guid campaignId, Guid entryId, object body)
    {
        fixture.LoginAsUser(user);
        var (status, text) = await fixture.Send(HttpMethod.Post, LinksUrl(campaignId, entryId), body);
        status.Should().Be(200, text);
        return JsonSerializer.Deserialize<EntryResponse>(text, JsonOptions)!;
    }

    private async Task<(int Status, string Body)> Refused(Users user, Guid campaignId, Guid entryId, object body)
    {
        fixture.LoginAsUser(user);
        return await fixture.Send(HttpMethod.Post, LinksUrl(campaignId, entryId), body);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string ErrorKeys(string body)
    {
        using var json = JsonDocument.Parse(body);
        return !json.RootElement.TryGetProperty("errors", out var errors)
            ? ""
            : string.Join(",", errors.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ADmAddsAKnowledgeBaseLink_AndTheResponseResolvesIt()
    {
        var campaign = await TestCampaign.Create(fixture, "Post link: kb");
        var caves = await Entry(Users.DM, campaign.Id, "The Lint Caves");

        var after = await Post(Users.DM, campaign.Id, caves.Id, KnowledgeBaseLinkBody(Provider, Gremlin));

        var link = after.Links.Should().ContainSingle().Subject;
        link.Kind.Should().Be(EntryLinkKind.KnowledgeBase);
        link.Provider.Should().Be(Provider);
        link.ItemId.Should().Be(Gremlin);
        link.Name.Should().Be("Test Gremlin");
        link.Stale.Should().BeFalse();
        link.AddedByMemberId.Should().Be(campaign.DmMemberId);
        link.Id.Should().NotBeEmpty("DELETE has to have something to name");
    }

    [Fact]
    public async Task AnExternalLink_KeepsTheUrlAsTyped_AndItsLabel()
    {
        var campaign = await TestCampaign.Create(fixture, "Post link: external");
        var caves = await Entry(Users.Player, campaign.Id, "The Map Room");

        var after = await Post(Users.Player, campaign.Id, caves.Id, ExternalLinkBody("https://example.com/a/map?x=1#top", "  the map I drew  "));

        var link = after.Links.Should().ContainSingle().Subject;
        link.Url.Should().Be("https://example.com/a/map?x=1#top");
        link.Label.Should().Be("the map I drew", "the label is trimmed");
        link.Provider.Should().BeNull();
        link.Stale.Should().BeFalse();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/x")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("/not/absolute")]
    [InlineData("example.com/no-scheme")]
    public async Task AUrlThatIsNotHttpOrHttps_IsA400_FromTheValidator(string url)
    {
        var campaign = await TestCampaign.Create(fixture, $"Post link: scheme {url}");
        var caves = await Entry(Users.DM, campaign.Id, "Scheme " + url.Length);

        var (status, body) = await Refused(Users.DM, campaign.Id, caves.Id, ExternalLinkBody(url, "nope"));

        status.Should().Be(400);
        ErrorKeys(body).Should().Be("url");
    }

    [Fact]
    public async Task AUrlLongerThanTheCap_AndAMissingOne_AreBoth400()
    {
        var campaign = await TestCampaign.Create(fixture, "Post link: url length");
        var caves = await Entry(Users.DM, campaign.Id, "Long urls");

        var tooLong = "https://example.com/" + new string('a', EntryLinks.UrlMaxLength);
        tooLong.Length.Should().BeGreaterThan(EntryLinks.UrlMaxLength);
        (await Refused(Users.DM, campaign.Id, caves.Id, ExternalLinkBody(tooLong, "long"))).Status.Should().Be(400);

        // And one character under the cap is fine, so the cap is the cap and not an approximation.
        var justFits = "https://example.com/" + new string('a', EntryLinks.UrlMaxLength - "https://example.com/".Length);
        justFits.Length.Should().Be(EntryLinks.UrlMaxLength);
        (await Post(Users.DM, campaign.Id, caves.Id, ExternalLinkBody(justFits, "fits"))).Links.Should().ContainSingle();

        var (status, body) = await Refused(Users.DM, campaign.Id, caves.Id, ExternalLinkBody(null, "no url"));
        status.Should().Be(400);
        ErrorKeys(body).Should().Be("url");
    }

    [Fact]
    public async Task AnExternalLinkNeedsALabel_OfAtMostEightyCharacters()
    {
        var campaign = await TestCampaign.Create(fixture, "Post link: label");
        var caves = await Entry(Users.DM, campaign.Id, "Labels");

        foreach (var label in new[] { null, "", "   " })
        {
            var (status, body) = await Refused(Users.DM, campaign.Id, caves.Id, ExternalLinkBody("https://example.com/1", label));
            status.Should().Be(400);
            ErrorKeys(body).Should().Be("label");
        }

        var tooLong = new string('x', EntryLinks.LabelMaxLength + 1);
        (await Refused(Users.DM, campaign.Id, caves.Id, ExternalLinkBody("https://example.com/2", tooLong))).Status.Should().Be(400);

        var exact = new string('x', EntryLinks.LabelMaxLength);
        (await Post(Users.DM, campaign.Id, caves.Id, ExternalLinkBody("https://example.com/3", exact)))
            .Links.Should().ContainSingle().Which.Label.Should().Be(exact);

        // A label is never markdown, so whatever it holds comes back unchanged and unescaped.
        var markdown = "**bold** [x](javascript:alert(1))";
        (await Post(Users.DM, campaign.Id, caves.Id, ExternalLinkBody("https://example.com/4", markdown)))
            .Links.Should().ContainSingle(l => l.Label == markdown);
    }

    [Fact]
    public async Task AKnowledgeBaseLinkNeedsARegisteredProvider_AndARowThatIsThere()
    {
        var campaign = await TestCampaign.Create(fixture, "Post link: kb missing");
        var caves = await Entry(Users.DM, campaign.Id, "Unknown rows");

        var unknownProvider = await Refused(Users.DM, campaign.Id, caves.Id, KnowledgeBaseLinkBody("not-a-provider", Gremlin));
        unknownProvider.Status.Should().Be(404);
        ErrorKeys(unknownProvider.Body).Should().Be("itemId");

        var unknownRow = await Refused(Users.DM, campaign.Id, caves.Id, KnowledgeBaseLinkBody(Provider, "monster_nope_tst"));
        unknownRow.Status.Should().Be(404);
        ErrorKeys(unknownRow.Body).Should().Be("itemId");

        foreach (var body in new[] { KnowledgeBaseLinkBody("", Gremlin), KnowledgeBaseLinkBody(Provider, "") })
        {
            (await Refused(Users.DM, campaign.Id, caves.Id, body)).Status.Should().Be(400);
        }
    }

    [Fact]
    public async Task ADuplicate_IsA409_RatherThanASilentNoOp()
    {
        var campaign = await TestCampaign.Create(fixture, "Post link: duplicates");
        var caves = await Entry(Users.DM, campaign.Id, "Twice over");

        await Post(Users.DM, campaign.Id, caves.Id, KnowledgeBaseLinkBody(Provider, Gremlin));
        var sameRow = await Refused(Users.DM, campaign.Id, caves.Id, KnowledgeBaseLinkBody(Provider, Gremlin));
        sameRow.Status.Should().Be(409);
        ErrorKeys(sameRow.Body).Should().Be("itemId");
        // The provider's key is compared case-insensitively, the way the catalog resolves it.
        (await Refused(Users.DM, campaign.Id, caves.Id, KnowledgeBaseLinkBody("5eTOOLS", Gremlin))).Status.Should().Be(409);
        // A different row is not a duplicate.
        (await Post(Users.DM, campaign.Id, caves.Id, KnowledgeBaseLinkBody(Provider, Chief))).Links.Should().HaveCount(2);

        await Post(Users.DM, campaign.Id, caves.Id, ExternalLinkBody("https://Example.com/Sheet/", "sheet"));
        var sameUrl = await Refused(Users.DM, campaign.Id, caves.Id, ExternalLinkBody("https://example.com/Sheet", "sheet again"));
        sameUrl.Status.Should().Be(409);
        ErrorKeys(sameUrl.Body).Should().Be("url");
        // The path's case is not folded, because many sites' paths are case-sensitive.
        (await Post(Users.DM, campaign.Id, caves.Id, ExternalLinkBody("https://example.com/sheet", "lower"))).Links.Should().HaveCount(4);
    }

    [Fact]
    public async Task TheTwentyFirstLink_IsA409()
    {
        var campaign = await TestCampaign.Create(fixture, "Post link: cap");
        var caves = await Entry(Users.DM, campaign.Id, "Many links");

        for (var i = 0; i < EntryLinks.MaxPerEntry; i++)
        {
            await Post(Users.DM, campaign.Id, caves.Id, ExternalLinkBody($"https://example.com/{i}", $"link {i}"));
        }

        var (status, body) = await Refused(Users.DM, campaign.Id, caves.Id, ExternalLinkBody("https://example.com/last", "one too many"));
        status.Should().Be(409);
        ErrorKeys(body).Should().Be("links");

        // The validator still runs first, so a 21st link that is also invalid is its 400: the cap is
        // the endpoint's rule and the scheme is the request's, and FastEndpoints checks the request
        // before the handler is entered at all.
        (await Refused(Users.DM, campaign.Id, caves.Id, ExternalLinkBody("javascript:alert(1)", "bad"))).Status.Should().Be(400);

        // And removing one makes room again.
        fixture.LoginAsUser(Users.DM);
        var entry = (await fixture.GetEntry(campaign.Id, caves.Id)).Value;
        (await fixture.DeleteEntryLink(campaign.Id, caves.Id, entry.Links[0].Id)).Should().Succeed();
        (await Post(Users.DM, campaign.Id, caves.Id, ExternalLinkBody("https://example.com/last", "room again"))).Links
            .Should().HaveCount(EntryLinks.MaxPerEntry);
    }

    [Fact]
    public async Task ANonEditor_IsA403_ForBothEndpoints()
    {
        var campaign = await TestCampaign.Create(fixture, "Post link: 403");
        var caves = await Entry(Users.Player, campaign.Id, "Only mine");
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutEntryEditAccess(campaign.Id, caves.Id, EditAccess.OnlyMe)).Should().Succeed();
        var link = (await Post(Users.Player, campaign.Id, caves.Id, ExternalLinkBody("https://example.com/mine", "mine"))).Links[0];

        (await Refused(Users.Outsider, campaign.Id, caves.Id, ExternalLinkBody("https://example.com/theirs", "theirs"))).Status.Should().Be(403);

        fixture.LoginAsUser(Users.Outsider);
        (await fixture.Send(HttpMethod.Delete, LinkUrl(campaign.Id, caves.Id, link.Id))).Status.Should().Be(403);

        // The DMs can always edit (invariant 4).
        fixture.LoginAsUser(Users.DM);
        (await fixture.DeleteEntryLink(campaign.Id, caves.Id, link.Id)).Value.Links.Should().BeEmpty();
    }

    /// <summary>
    /// The case that exposed <see cref="EntryLinks.CanWrite"/> (27d): <see cref="EntryPermissions.CanEdit"/>
    /// has no claimer clause, so a DM who creates an NPC, hands it to a player and then restricts edit
    /// access would have locked that player out of their own character's sheet link — the whole of 27e.
    /// The clause is <b>only</b> about links: the same player still cannot touch the article.
    /// </summary>
    [Fact]
    public async Task AClaimer_WritesItsLinks_EvenWithEditAccessRestricted_ButStillCannotEditTheArticle()
    {
        var campaign = await TestCampaign.Create(fixture, "Post link: the claimer");
        var thorin = await Entry(Users.DM, campaign.Id, "Thorin", EntryKind.Character);

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryClaim(campaign.Id, thorin.Id, campaign.PlayerMemberId)).Should().Succeed();
        (await fixture.PutEntryEditAccess(campaign.Id, thorin.Id, EditAccess.OnlyMe)).Should().Succeed();

        // The player is neither a DM nor the creator, and edit access is now OnlyMe.
        fixture.LoginAsUser(Users.Player);
        var view = (await fixture.GetEntry(campaign.Id, thorin.Id)).Value;
        (await fixture.Send(HttpMethod.Put, EntryUrl(campaign.Id, thorin.Id, "name"), new { name = "Thorin Oakenshield" }))
            .Status.Should().Be(403, "the claimer clause is about links, not the entry");
        (await fixture.Send(HttpMethod.Put, ArticleUrl(campaign.Id, thorin.Id),
                ArticleBody(view.Article.Etag, [new BlockEdit(null, "My own words")])))
            .Status.Should().Be(403, "article permissions are not widened");

        // And yet their own sheet link goes on and comes off again.
        var sheet = "https://www.dndbeyond.com/characters/12345678";
        var added = await Post(Users.Player, campaign.Id, thorin.Id, ExternalLinkBody(sheet, "D&D Beyond sheet"));
        var link = added.Links.Should().ContainSingle().Subject;
        link.Url.Should().Be(sheet);
        link.AddedByMemberId.Should().Be(campaign.PlayerMemberId);

        fixture.LoginAsUser(Users.Player);
        (await fixture.DeleteEntryLink(campaign.Id, thorin.Id, link.Id)).Value.Links.Should().BeEmpty();

        // Unclaimed again, the clause is gone and so is the player's access.
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryClaim(campaign.Id, thorin.Id, null)).Should().Succeed();
        (await Refused(Users.Player, campaign.Id, thorin.Id, ExternalLinkBody(sheet, "D&D Beyond sheet"))).Status.Should().Be(403);
    }

    [Fact]
    public async Task DeletingAnUnknownLink_IsA404_AndSoIsOneTheCallerMayNotRead()
    {
        var campaign = await TestCampaign.Create(fixture, "Delete link: 404");
        var eye = await Entry(Users.DM, campaign.Id, "The Eye", EntryKind.Character);
        var link = (await Post(Users.DM, campaign.Id, eye.Id, KnowledgeBaseLinkBody(Provider, Gremlin))).Links[0];

        fixture.LoginAsUser(Users.DM);
        (await fixture.Send(HttpMethod.Delete, LinkUrl(campaign.Id, eye.Id, Guid.NewGuid()))).Status.Should().Be(404);

        // A player can edit this entry (its edit access is Anyone) but may not read its links, so
        // naming one must not tell them it is there.
        fixture.LoginAsUser(Users.Player);
        (await fixture.Send(HttpMethod.Delete, LinkUrl(campaign.Id, eye.Id, link.Id))).Status.Should().Be(404);

        fixture.LoginAsUser(Users.DM);
        (await fixture.DeleteEntryLink(campaign.Id, eye.Id, link.Id)).Value.Links.Should().BeEmpty();
        (await fixture.Send(HttpMethod.Delete, LinkUrl(campaign.Id, eye.Id, link.Id))).Status.Should().Be(404, "it is gone now");
    }

    [Fact]
    public async Task AnNpcsLink_PushesToTheDmsAlone_AndAClaimedOnesToEveryone()
    {
        var campaign = await TestCampaign.Create(fixture, "Post link: push");
        var eye = await Entry(Users.DM, campaign.Id, "The Watcher", EntryKind.Character);

        var mark = fixture.Hub.Messages.Count;
        var link = (await Post(Users.DM, campaign.Id, eye.Id, KnowledgeBaseLinkBody(Provider, Gremlin))).Links[0];
        var ping = fixture.Hub.Messages.Skip(mark).Should().ContainSingle().Subject;
        ping.Method.Should().Be(CampaignHubMessages.EntryLinksChanged);
        ping.Groups.Should().Equal([CampaignGroups.Member(campaign.DmMemberId)], "a player must not even learn that a link appeared");

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryClaim(campaign.Id, eye.Id, campaign.PlayerMemberId)).Should().Succeed();

        mark = fixture.Hub.Messages.Count;
        fixture.LoginAsUser(Users.DM);
        (await fixture.DeleteEntryLink(campaign.Id, eye.Id, link.Id)).Should().Succeed();
        var afterClaim = fixture.Hub.Messages.Skip(mark).Should().ContainSingle().Subject;
        afterClaim.Method.Should().Be(CampaignHubMessages.EntryLinksChanged);
        afterClaim.Groups.Should().BeEquivalentTo([
            CampaignGroups.Member(campaign.DmMemberId),
            CampaignGroups.Member(campaign.PlayerMemberId),
            CampaignGroups.Member(campaign.SecondPlayerMemberId!.Value),
        ]);
    }

    [Fact]
    public async Task AClaimPushesTheLinks_ToTheMembersItShowedThemTo()
    {
        var campaign = await TestCampaign.Create(fixture, "Post link: claim push");
        var eye = await Entry(Users.DM, campaign.Id, "The Second Watcher", EntryKind.Character);
        await Post(Users.DM, campaign.Id, eye.Id, KnowledgeBaseLinkBody(Provider, Gremlin));

        var mark = fixture.Hub.Messages.Count;
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryClaim(campaign.Id, eye.Id, campaign.PlayerMemberId)).Should().Succeed();

        var ping = fixture.Hub.Messages.Skip(mark)
            .Should().ContainSingle(m => m.Method == CampaignHubMessages.EntryLinksChanged).Subject;
        ping.Groups.Should().BeEquivalentTo([
            CampaignGroups.Member(campaign.PlayerMemberId),
            CampaignGroups.Member(campaign.SecondPlayerMemberId!.Value),
        ], "the DMs could read them all along");
    }
}
