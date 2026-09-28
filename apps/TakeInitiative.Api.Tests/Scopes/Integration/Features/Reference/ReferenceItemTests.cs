using FluentAssertions;
using Microsoft.Extensions.Primitives;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Reference;

namespace TakeInitiative.Api.Tests.Integration.Features.Reference;

/// <summary>
/// <c>GET /api/reference/{provider}/{itemId}</c> (20b.2): any signed-in user, no campaign, the
/// stat block with its attribution, a 404 for anything it does not know, and a day's caching.
/// </summary>
public class ReferenceItemTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private static string Url(string provider, string id) => $"/api/reference/{provider}/{id}";

    [Fact]
    public async Task AnItem_IsItsStatBlock_WithTheAttribution_ForAnySignedInUser()
    {
        foreach (var who in new[] { Users.DM, Users.Stranger })
        {
            fixture.LoginAsUser(who);
            var result = await fixture.AlbaHost.Scenario(_ =>
            {
                _.Get.Url(Url("srd52", "goblin-warrior"));
                _.StatusCodeShouldBe(200);
            });
            result.Context.Response.Headers.CacheControl.ToString().Should().Be(GetReferenceItem.CacheControl);

            var item = (await result.ReadAsJsonAsync<ReferenceItemResponse>())!;
            item.Summary.Should().Match<ReferenceSummaryResponse>(s =>
                s.Provider == "srd52" && s.ProviderLabel == "SRD 5.2" && s.Id == "goblin-warrior"
                && s.Name == "Goblin Warrior" && s.HasStatBlock && s.SuggestedKind == EntryKind.Character);
            item.Summary.Stats.Should().Be(new StatsResponse { InitiativeRoll = "1d20+2", MaxHp = "3d6", Ac = 15 });
            item.StatBlock.Ac.Should().Be(15);
            item.StatBlock.HitDice.Should().Be("3d6");
            item.StatBlock.Cr.Should().Be("1/4");
            item.StatBlock.Actions.Select(a => (a.Kind, a.Name)).Should().Contain(
                [(StatBlockActionKind.Action, "Scimitar"), (StatBlockActionKind.BonusAction, "Nimble Escape")]);
            item.Attribution.Text.Should().Contain("System Reference Document 5.2").And.Contain("Creative Commons");
            item.Attribution.LicenseUrl.Should().StartWith("https://creativecommons.org/");
        }
    }

    [Fact]
    public async Task TheProviderKey_IsCaseInsensitive_AndTheJsonUsesStrings()
    {
        fixture.LoginAsUser(Users.Player);
        var result = await fixture.AlbaHost.Scenario(_ =>
        {
            _.Get.Url(Url("SRD52", "owlbear"));
            _.StatusCodeShouldBe(200);
        });
        var body = await result.ReadAsTextAsync();
        body.Should().Contain("\"kind\":\"Action\"").And.Contain("\"category\":\"Monster\"");
    }

    [Theory]
    [InlineData("srd52", "not-a-monster")]
    [InlineData("srd51", "goblin-warrior")]
    [InlineData("5etools", "goblin-warrior")]
    public async Task AnUnknownProviderOrItem_IsA404(string provider, string id)
    {
        fixture.LoginAsUser(Users.Player);
        (await fixture.GetStatus(Url(provider, id))).Should().Be(404);
    }

    [Fact]
    public async Task SignedOut_IsA401()
    {
        var result = await fixture.AlbaHost.Scenario(_ =>
        {
            _.Get.Url(Url("srd52", "goblin-warrior"));
            _.ConfigureHttpContext(c => c.Request.Headers.Cookie = StringValues.Empty);
            _.IgnoreStatusCode();
        });
        result.Context.Response.StatusCode.Should().Be(401);
    }
}
