using System.Text.Json.Serialization;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Connections;

public record GetConnectionEvidenceRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    public Guid OtherEntryId { get; init; }
}

public record ConnectionEvidenceResponse
{
    public required EntrySummaryResponse From { get; init; }
    public required EntrySummaryResponse To { get; init; }
    /// <summary>Blocks first (in article order, by entry name), then notes newest first, then combats newest first. As many rows as the pair's weight.</summary>
    public required EvidenceResponse[] Evidence { get; init; }
}

/// <summary>One piece of evidence (glossary: Evidence). Exactly one of <see cref="Block"/>, <see cref="Note"/> and <see cref="Combat"/> is set, per <see cref="Kind"/>.</summary>
public record EvidenceResponse
{
    public required EvidenceKind Kind { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BlockEvidence? Block { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NoteEvidence? Note { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CombatEvidence? Combat { get; init; }
}

public record BlockEvidence
{
    /// <summary>The entry whose article holds the block.</summary>
    public required Guid EntryId { get; init; }
    public required string EntryName { get; init; }
    public required Guid BlockId { get; init; }
    /// <summary>The block's own visibility, so the web can mark a secret block 🔒.</summary>
    public required Visibility Visibility { get; init; }
    /// <summary>The block's markdown around the two entries (<see cref="EvidenceSnippet"/>), mentions kept as <c>@[text](entry:id)</c>.</summary>
    public required string Snippet { get; init; }
}

public record NoteEvidence
{
    public required Guid NoteId { get; init; }
    public required Guid SessionId { get; init; }
    public required int SessionNumber { get; init; }
    public required Guid AuthorMemberId { get; init; }
    public required Visibility Visibility { get; init; }
    /// <summary>Only a DM or the author ever gets a hidden note.</summary>
    public required bool IsHidden { get; init; }
    public required DateTimeOffset PostedAt { get; init; }
    public required bool HasImages { get; init; }
    /// <summary>The note's markdown around the two entries (<see cref="EvidenceSnippet"/>), mentions kept as <c>@[text](entry:id)</c>.</summary>
    public required string Snippet { get; init; }
}

public record CombatEvidence
{
    /// <summary>The combat as the viewer sees it (<see cref="CombatCard.For"/>).</summary>
    public required CombatCard Card { get; init; }
    public required int SessionNumber { get; init; }
}

/// <summary>
/// The evidence for one connection (19a.4): the article blocks, notes and started combats the
/// caller can see in which both entries occur, from the same <see cref="ConnectionPairs"/> run
/// as the weight, so the rows always number the weight. Both entries must be visible (404
/// otherwise; merged ids answer for their targets). A visible pair with no connection is a 200
/// with no evidence, so the answer never says why.
/// </summary>
public class GetConnectionEvidence(IDocumentSession session) : Endpoint<GetConnectionEvidenceRequest, ConnectionEvidenceResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/entries/{EntryId}/connections/{OtherEntryId}");
    }

    public override async Task HandleAsync(GetConnectionEvidenceRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var from = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);
        var to = await this.RequireVisibleEntry(session, req.CampaignId, req.OtherEntryId, member, ct);

        var read = await ConnectionIndex.ForEntry(session, req.CampaignId, from, member, ct);
        var sources = from.Id != to.Id && read.Pairs.TryGetValue(new EntryPair(from.Id, to.Id), out var connection)
            ? connection.Evidence
            : [];

        var fromIds = from.MentionIds();
        var toIds = to.MentionIds();

        var blocks = sources
            .Where(s => s.Kind == EvidenceKind.Block)
            .Select(s => (Source: s, Entry: read.Entries[s.ArticleEntryId!.Value]))
            .Select(x => (x.Entry, Index: IndexOf(x.Entry, x.Source.Id), Block: x.Entry.Article.Blocks.First(b => b.Id == x.Source.Id)))
            .OrderBy(x => x.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Entry.Id)
            .ThenBy(x => x.Index)
            .Select(x => new EvidenceResponse
            {
                Kind = EvidenceKind.Block,
                Block = new BlockEvidence
                {
                    EntryId = x.Entry.Id,
                    EntryName = x.Entry.Name,
                    BlockId = x.Block.Id,
                    Visibility = x.Block.Visibility,
                    Snippet = EvidenceSnippet.Cut(x.Block.Text, fromIds, toIds),
                },
            })
            .ToList();

        var noteIds = sources.Where(s => s.Kind == EvidenceKind.Note).Select(s => s.Id).ToArray();
        var notes = noteIds.Length == 0 ? [] : await session.LoadManyAsync<SessionNote>(ct, noteIds);

        var combats = sources
            .Where(s => s.Kind == EvidenceKind.Combat)
            .Select(s => read.Combats[s.Id])
            .OrderByDescending(c => c.StartedAt)
            .ToList();
        var cards = await CombatCard.For(session, combats, member, ct);

        var sessionIds = notes.Select(n => n.SessionId).Concat(cards.Select(c => c.SessionId)).Distinct().ToArray();
        var numbers = sessionIds.Length == 0
            ? new Dictionary<Guid, int>()
            : (await session.LoadManyAsync<Session>(ct, sessionIds)).ToDictionary(s => s.Id, s => s.Number);

        var noteRows = notes
            .OrderByDescending(n => n.PostedAt)
            .Select(n => new EvidenceResponse
            {
                Kind = EvidenceKind.Note,
                Note = new NoteEvidence
                {
                    NoteId = n.Id,
                    SessionId = n.SessionId,
                    SessionNumber = numbers.GetValueOrDefault(n.SessionId),
                    AuthorMemberId = n.AuthorMemberId,
                    Visibility = n.Visibility,
                    IsHidden = n.IsHidden,
                    PostedAt = n.PostedAt,
                    HasImages = n.HasImages,
                    Snippet = EvidenceSnippet.Cut(n.Text, fromIds, toIds),
                },
            });

        var combatRows = cards.Select(c => new EvidenceResponse
        {
            Kind = EvidenceKind.Combat,
            Combat = new CombatEvidence { Card = c, SessionNumber = numbers.GetValueOrDefault(c.SessionId) },
        });

        await SendAsync(new ConnectionEvidenceResponse
        {
            From = EntrySummaryResponse.From(from),
            To = EntrySummaryResponse.From(to),
            Evidence = [.. blocks, .. noteRows, .. combatRows],
        }, cancellation: ct);
    }

    private static int IndexOf(Entry entry, Guid blockId)
    {
        for (var i = 0; i < entry.Article.Blocks.Count; i++)
        {
            if (entry.Article.Blocks[i].Id == blockId) return i;
        }
        return int.MaxValue;
    }
}
