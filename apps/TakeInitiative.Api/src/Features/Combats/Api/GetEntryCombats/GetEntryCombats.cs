using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record GetEntryCombatsRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
}

public record EntryCombatsResponse
{
    /// <summary>Newest first (<c>startedAt ?? createdAt</c>).</summary>
    public required EntryCombat[] Combats { get; init; }
}

public record EntryCombat
{
    public required CombatCard Combat { get; init; }
    public required int SessionNumber { get; init; }
}

/// <summary>
/// An entry's COMBATS (18e.4, design §4): the combats in which the entry, or an entry merged
/// into it, is a combatant the caller can see. Found with the GIN index on
/// <see cref="Combat.EntryIds"/>, then checked against the caller's own view, so a player never
/// gets a combat through a hidden combatant, nor a Draft. An entry the caller cannot see is a 404.
/// </summary>
public class GetEntryCombats(IDocumentSession session) : Endpoint<GetEntryCombatsRequest, EntryCombatsResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/entries/{EntryId}/combats");
    }

    public override async Task HandleAsync(GetEntryCombatsRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);

        // A combatant keeps the id it was added with, so a merged entry's combats are this one's (15g).
        var ids = entry.MentionIds().ToHashSet();
        var combats = await session.Query<Combat>()
            .Where(c => c.CampaignId == req.CampaignId)
            .Where(MentionIndex.MentioningAny<Combat>(nameof(Combat.EntryIds), ids))
            .ToListAsync(ct);
        var through = combats
            .Where(c => CombatView.VisibleCombatants(c, member).Any(x => x.EntryId is { } id && ids.Contains(id)))
            .ToList();
        var cards = await CombatCard.For(session, through, member, ct);

        var sessionIds = cards.Select(c => c.SessionId).Distinct().ToArray();
        var numbers = sessionIds.Length == 0
            ? new Dictionary<Guid, int>()
            : (await session.LoadManyAsync<Session>(ct, sessionIds)).ToDictionary(s => s.Id, s => s.Number);

        await SendAsync(new EntryCombatsResponse
        {
            Combats = cards
                .OrderByDescending(c => c.StartedAt ?? c.CreatedAt)
                .Select(c => new EntryCombat { Combat = c, SessionNumber = numbers.GetValueOrDefault(c.SessionId) })
                .ToArray(),
        }, cancellation: ct);
    }
}
