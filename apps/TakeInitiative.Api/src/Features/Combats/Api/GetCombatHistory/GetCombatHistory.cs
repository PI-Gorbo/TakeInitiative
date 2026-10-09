using System.Text.Json.Serialization;
using FastEndpoints;
using Marten;
using JasperFx.Events;
using IEvent = JasperFx.Events.IEvent;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record GetCombatHistoryRequest
{
    public Guid CampaignId { get; init; }
    public Guid CombatId { get; init; }
}

public record CombatHistoryResponse
{
    /// <summary>Every event on the combat's stream, oldest first.</summary>
    public required CombatHistoryItem[] Items { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<CombatHistoryKind>))]
public enum CombatHistoryKind
{
    Created,
    CombatantsAdded,
    CombatantEdited,
    CombatantRemoved,
    InitiativeRolled,
    TurnEnded,
    Finished,
}

/// <summary>One event: its version on the stream, when, by whom, and a sentence saying what happened.</summary>
public record CombatHistoryItem
{
    public required long Version { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required Guid ActorMemberId { get; init; }
    public required CombatHistoryKind Kind { get; init; }
    public required string Text { get; init; }
}

/// <summary>
/// A combat's history (18b.6), for DMs only: players get 403, since redacting past events per
/// viewer (a hidden goblin's roll in an old <see cref="InitiativeRolled"/>) is not in step 18.
/// It reads the stream directly (<c>FetchStreamAsync</c>); no history is stored. Each row's
/// text is built here, naming the actor and the combatants as they were named then.
/// </summary>
public class GetCombatHistory(IDocumentSession session) : Endpoint<GetCombatHistoryRequest, CombatHistoryResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/combats/{CombatId}/history");
    }

    public override async Task HandleAsync(GetCombatHistoryRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var combat = await this.RequireVisibleCombat(session, req.CampaignId, req.CombatId, member, ct);
        this.RequireDm(member, "Only a DM can read a combat's history.");

        var userIds = campaign.Members.Select(m => m.UserId).ToArray();
        var usernames = (await session.Query<ApplicationUser>()
                .Where(u => u.Id.IsOneOf(userIds))
                .Select(u => new { u.Id, u.UserName })
                .ToListAsync(ct))
            .ToDictionary(u => u.Id, u => u.UserName ?? "");
        var names = campaign.Members.ToDictionary(m => m.MemberId, m => usernames.GetValueOrDefault(m.UserId, ""));

        var events = await session.Events.FetchStreamAsync(combat.Id, token: ct);
        await SendAsync(new CombatHistoryResponse { Items = CombatHistory.For(events, names).ToArray() }, cancellation: ct);
    }
}

/// <summary>The history rows of a combat stream, pure over its events and the members' names.</summary>
public static class CombatHistory
{
    public static IEnumerable<CombatHistoryItem> For(IReadOnlyList<IEvent> events, IReadOnlyDictionary<Guid, string> memberNames)
    {
        // Replayed as it goes, so each sentence uses the names and values of its moment.
        Combat? combat = null;
        foreach (var @event in events)
        {
            var actorId = ((IActorEvent)@event.Data).Actor.MemberId;
            var actor = memberNames.TryGetValue(actorId, out var name) && name.Length > 0 ? name : "A former member";
            var before = combat;
            combat = @event.Data switch
            {
                CombatCreated => Combat.Create((IEvent<CombatCreated>)@event),
                CombatantsAdded e => combat!.Apply(e),
                CombatantEdited e => combat!.Apply(e),
                CombatantRemoved e => combat!.Apply(e),
                InitiativeRolled => combat!.Apply((IEvent<InitiativeRolled>)@event),
                TurnEnded e => combat!.Apply(e),
                CombatFinished => combat!.Apply((IEvent<CombatFinished>)@event),
                _ => combat,
            };

            (CombatHistoryKind Kind, string Text)? row = @event.Data switch
            {
                CombatCreated e => (CombatHistoryKind.Created, $"{actor} created {e.Name}."),
                CombatantsAdded e => (CombatHistoryKind.CombatantsAdded, $"{actor} added {List(e.Combatants.Select(c => c.Name))}."),
                CombatantEdited e => (CombatHistoryKind.CombatantEdited, Edited(actor, before!.Find(e.CombatantId), e)),
                CombatantRemoved e => (CombatHistoryKind.CombatantRemoved, $"{actor} removed {NameOf(before!, e.CombatantId)}."),
                InitiativeRolled e => (CombatHistoryKind.InitiativeRolled, Rolled(actor, before!, e)),
                TurnEnded e => (CombatHistoryKind.TurnEnded, TurnEnded(actor, before!, e)),
                CombatFinished => (CombatHistoryKind.Finished, $"{actor} finished the combat."),
                _ => null,
            };
            if (row is { } r)
            {
                yield return new CombatHistoryItem
                {
                    Version = @event.Version,
                    Timestamp = @event.Timestamp,
                    ActorMemberId = actorId,
                    Kind = r.Kind,
                    Text = r.Text,
                };
            }
        }
    }

    private static string Rolled(string actor, Combat before, InitiativeRolled e)
    {
        var rolls = List(e.Rolls.Select(r => $"{NameOf(before, r.CombatantId)} rolled {r.Total}"));
        if (before.Status == CombatStatus.Draft)
        {
            return e.Rolls.Length == 0 ? $"{actor} started the combat." : $"{actor} started the combat: {rolls}.";
        }
        return $"{actor} rolled initiative: {rolls}.";
    }

    private static string TurnEnded(string actor, Combat before, TurnEnded e)
    {
        var text = $"{actor} ended {NameOf(before, e.FromCombatantId)}'s turn.";
        if (e.Round != before.Round)
        {
            text += $" Round {e.Round}.";
        }
        if (e.ToCombatantId is { } to)
        {
            text += $" Next: {NameOf(before, to)}.";
        }
        return text;
    }

    private static string Edited(string actor, Combatant? before, CombatantEdited e)
    {
        if (before is null)
        {
            return $"{actor} edited a combatant.";
        }
        var after = e.State;
        var changes = new List<string>();
        if (after.Name != before.Name) changes.Add($"renamed to {after.Name}");
        if (after.Initiative != before.Initiative) changes.Add($"initiative {Value(before.Initiative)} → {Value(after.Initiative)}");
        if (after.Hp != before.Hp) changes.Add($"HP {Value(before.Hp)} → {Value(after.Hp)}");
        if (after.MaxHp != before.MaxHp) changes.Add($"max HP {Value(before.MaxHp)} → {Value(after.MaxHp)}");
        if (after.Ac != before.Ac) changes.Add($"AC {Value(before.Ac)} → {Value(after.Ac)}");
        if (after.Hidden != before.Hidden) changes.Add(after.Hidden ? "hidden" : "revealed");
        if (after.PlayersSee != before.PlayersSee) changes.Add($"players see {after.PlayersSee}");
        var added = after.Conditions.Select(c => c.Label).Except(before.Conditions.Select(c => c.Label)).ToList();
        var removed = before.Conditions.Select(c => c.Label).Except(after.Conditions.Select(c => c.Label)).ToList();
        if (added.Count > 0) changes.Add($"+{string.Join(", +", added)}");
        if (removed.Count > 0) changes.Add($"−{string.Join(", −", removed)}");

        if (changes.Count == 0)
        {
            return e.Tiebreak is not null ? $"{actor} moved {before.Name} in the order." : $"{actor} edited {before.Name}.";
        }
        return $"{actor} changed {before.Name}: {string.Join(", ", changes)}.";
    }

    private static string Value(int? value) => value?.ToString() ?? "none";

    private static string NameOf(Combat combat, Guid combatantId) => combat.Find(combatantId)?.Name ?? "a removed combatant";

    /// <summary>"A", "A and B", "A, B and C".</summary>
    private static string List(IEnumerable<string> names)
    {
        var all = names.ToList();
        return all.Count switch
        {
            0 => "no one",
            1 => all[0],
            _ => $"{string.Join(", ", all.Take(all.Count - 1))} and {all[^1]}",
        };
    }
}
