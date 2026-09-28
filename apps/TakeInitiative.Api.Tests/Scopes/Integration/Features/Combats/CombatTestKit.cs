using System.Text.Json;
using FakeItEasy;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Tests.Integration.Features.Combats;

/// <summary>
/// Calls and seeds for the combat tests (step 18a). Each call returns the status and the raw
/// body as well as the parsed response, so the leak tests can search the JSON a player got.
/// </summary>
public static class CombatTestKit
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>The fixture's dice roller is a fake: point it at a real one.</summary>
    public static void UseRealDice(AuthenticatedWebAppWithDatabaseFixture fixture)
    {
        var real = new DiceRoller(Random.Shared);
        A.CallTo(() => fixture.DiceRoller.Check(A<string>._)).ReturnsLazily((string roll) => real.Check(roll));
        A.CallTo(() => fixture.DiceRoller.EvaluateRoll(A<string>._)).ReturnsLazily((string roll) => real.EvaluateRoll(roll));
    }

    public static string CombatsUrl(Guid campaignId) => $"/api/campaigns/{campaignId}/combats";
    public static string CombatUrl(Guid campaignId, Guid combatId, string? part = null)
        => $"{CombatsUrl(campaignId)}/{combatId}" + (part is null ? "" : $"/{part}");
    public static string CombatantUrl(Guid campaignId, Guid combatId, Guid combatantId)
        => CombatUrl(campaignId, combatId, $"combatants/{combatantId}");

    public record Reply(int Status, string Body)
    {
        public T As<T>() => JsonSerializer.Deserialize<T>(Body, Web)!;
        public CombatResponse Combat => As<CombatResponse>();
    }

    public static async Task<Reply> Call(this IWebAppClient client, HttpMethod method, string url, object? body = null)
    {
        var result = await client.AlbaHost.Scenario(_ =>
        {
            if (method == HttpMethod.Get) _.Get.Url(url);
            else if (method == HttpMethod.Post) _.Post.Json(body ?? new { }).ToUrl(url);
            else if (method == HttpMethod.Put) _.Put.Json(body ?? new { }).ToUrl(url);
            else if (method == HttpMethod.Delete) _.Delete.Url(url);
            else throw new NotSupportedException(method.ToString());
            _.IgnoreStatusCode();
        });
        return new Reply(result.Context.Response.StatusCode, await result.ReadAsTextAsync());
    }

    public static async Task<Reply> Ok(this Task<Reply> call)
    {
        var reply = await call;
        reply.Status.Should().Be(200, reply.Body);
        return reply;
    }

    public static async Task<CombatResponse> CreateCombat(this AuthenticatedWebAppWithDatabaseFixture fixture, Guid campaignId, string name = "Goblin Ambush")
    {
        fixture.LoginAsUser(Users.DM);
        return (await fixture.Call(HttpMethod.Post, CombatsUrl(campaignId), new { name }).Ok()).Combat;
    }

    public static Task<Reply> GetCombat(this IWebAppClient client, Guid campaignId, Guid combatId)
        => client.Call(HttpMethod.Get, CombatUrl(campaignId, combatId));

    public static Task<Reply> GetCombats(this IWebAppClient client, Guid campaignId, string? status = null)
        => client.Call(HttpMethod.Get, CombatsUrl(campaignId) + (status is null ? "" : $"?status={status}"));

    public static Task<Reply> AddCombatants(this IWebAppClient client, Guid campaignId, Guid combatId, params object[] combatants)
        => client.Call(HttpMethod.Post, CombatUrl(campaignId, combatId, "combatants"), new { combatants });

    public static async Task<CombatResponse> AddAsDm(this AuthenticatedWebAppWithDatabaseFixture fixture, Guid campaignId, Guid combatId, params object[] combatants)
    {
        fixture.LoginAsUser(Users.DM);
        return (await fixture.AddCombatants(campaignId, combatId, combatants).Ok()).Combat;
    }

    public static Task<Reply> PutCombatant(this IWebAppClient client, Guid campaignId, Guid combatId, Guid combatantId, object body)
        => client.Call(HttpMethod.Put, CombatantUrl(campaignId, combatId, combatantId), body);

    public static Task<Reply> DeleteCombatant(this IWebAppClient client, Guid campaignId, Guid combatId, Guid combatantId)
        => client.Call(HttpMethod.Delete, CombatantUrl(campaignId, combatId, combatantId));

    /// <summary>A <c>PUT</c> body that keeps everything of <paramref name="c"/>, with changes on top.</summary>
    public static Dictionary<string, object?> Body(CombatantResponse c, Action<Dictionary<string, object?>>? change = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = c.Name,
            ["initiative"] = c.Initiative,
            ["hp"] = c.Hp,
            ["maxHp"] = c.MaxHp,
            ["ac"] = c.Ac,
            ["hidden"] = c.Hidden,
            ["playersSee"] = c.PlayersSee.ToString(),
            ["conditions"] = c.Conditions.Select(x => new { label = x.Label, note = x.Note }).ToArray(),
        };
        change?.Invoke(body);
        return body;
    }

    // 18b: roll, end turn, reorder, finish and history.

    public static Task<Reply> Roll(this IWebAppClient client, Guid campaignId, Guid combatId)
        => client.Call(HttpMethod.Post, CombatUrl(campaignId, combatId, "roll"));

    public static Task<Reply> EndTurn(this IWebAppClient client, Guid campaignId, Guid combatId, Guid combatantId, int round)
        => client.Call(HttpMethod.Post, CombatUrl(campaignId, combatId, "end-turn"), new { combatantId, round });

    /// <summary>Ends whoever's turn it is, as the caller, and answers the combat after it.</summary>
    public static async Task<CombatResponse> EndCurrentTurn(this IWebAppClient client, Guid campaignId, CombatResponse combat)
        => (await client.EndTurn(campaignId, combat.Id, combat.TurnCombatantId!.Value, combat.Round).Ok()).Combat;

    public static Task<Reply> Finish(this IWebAppClient client, Guid campaignId, Guid combatId)
        => client.Call(HttpMethod.Post, CombatUrl(campaignId, combatId, "finish"));

    public static Task<Reply> Position(this IWebAppClient client, Guid campaignId, Guid combatId, Guid combatantId, Guid? afterId)
        => client.Call(HttpMethod.Put, CombatantUrl(campaignId, combatId, combatantId) + "/position", new { afterId });

    public static Task<Reply> History(this IWebAppClient client, Guid campaignId, Guid combatId)
        => client.Call(HttpMethod.Get, CombatUrl(campaignId, combatId, "history"));

    /// <summary>Names in the order, then the waiting ones, as the response lists them.</summary>
    public static string[] Names(this CombatResponse combat) => combat.Combatants.Select(c => c.Name).ToArray();

    public static CombatantResponse Named(this CombatResponse combat, string name) => combat.Combatants.Single(c => c.Name == name);

    /// <summary>
    /// Starts a combat the way 18b's first roll will: one <see cref="InitiativeRolled"/> giving
    /// each named combatant its initiative. 18a has no roll endpoint, so it is appended here.
    /// </summary>
    public static async Task Start(this AuthenticatedWebAppWithDatabaseFixture fixture, Guid combatId, IReadOnlyDictionary<Guid, int> initiatives)
    {
        var store = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var combat = (await session.LoadAsync<Combat>(combatId))!;
        session.Events.Append(combatId, new InitiativeRolled(
            Actor.Member(combat.CreatedByMemberId),
            null,
            initiatives.Select(p => new InitiativeRollResult(p.Key, p.Value, $"{p.Value}", $"{p.Value}")).ToArray()));
        await session.SaveChangesAsync();
    }

    /// <summary>Creates an entry as <paramref name="who"/>, optionally with Stats set by the DM.</summary>
    public static async Task<EntryResponse> Entry(
        this AuthenticatedWebAppWithDatabaseFixture fixture, Users who, Guid campaignId, string name,
        Visibility visibility = Visibility.Everyone, EntryKind kind = EntryKind.Character,
        (string? Initiative, string? MaxHp, int? Ac)? stats = null)
    {
        fixture.LoginAsUser(who);
        var entry = await fixture.PostEntry(campaignId, name, kind, visibility);
        entry.Should().Succeed();
        if (stats is { } s)
        {
            fixture.LoginAsUser(Users.DM);
            (await fixture.PutEntryStats(campaignId, entry.Value.Id, s.Initiative, s.MaxHp, s.Ac)).Should().Succeed();
        }
        return entry.Value;
    }

    /// <summary>A Character <see cref="Users.Player"/> has claimed, with Stats.</summary>
    public static async Task<EntryResponse> PlayerCharacter(this AuthenticatedWebAppWithDatabaseFixture fixture, TestCampaign campaign, string name = "Brynn")
    {
        var entry = await fixture.Entry(Users.Player, campaign.Id, name);
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutEntryClaim(campaign.Id, entry.Id, campaign.PlayerMemberId)).Should().Succeed();
        (await fixture.PutEntryStats(campaign.Id, entry.Id, "1d20+3", "24", 15)).Should().Succeed();
        return entry;
    }
}
