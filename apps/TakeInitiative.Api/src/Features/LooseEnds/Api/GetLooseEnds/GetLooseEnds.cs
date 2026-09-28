using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.LooseEnds;

public record GetLooseEndsRequest
{
    public Guid CampaignId { get; init; }
    /// <summary>Only this session's loose ends: one divider's. Entries with no session are then left out.</summary>
    public Guid? SessionId { get; init; }
}

public record LooseEndsResponse
{
    /// <summary>Notes newest first, then entries by mention count (most first), then name.</summary>
    public required LooseEndResponse[] Items { get; init; }
}

/// <summary>One loose end: a note (<see cref="Note"/>) or an entry (<see cref="Entry"/>).</summary>
public record LooseEndResponse
{
    public required LooseEndKind Kind { get; init; }
    /// <summary>The note's session, or the session of the note an entry was created from; null for an entry made in the Wiki.</summary>
    public Guid? SessionId { get; init; }
    public int? SessionNumber { get; init; }
    /// <summary>The note, for <c>UnlinkedNote</c> and <c>UntaggedImageNote</c>.</summary>
    public SessionNoteResponse? Note { get; init; }
    /// <summary>Spans of the note's text the entry matcher matched: "this says Gundren, link it?". Empty for an entry.</summary>
    public required LinkSuggestionResponse[] Suggestions { get; init; }
    /// <summary>The entry, for <c>OtherKind</c> and <c>EmptyArticle</c>.</summary>
    public EntrySummaryResponse? Entry { get; init; }
    /// <summary>How many notes and blocks the caller can see mention the entry; null for a note.</summary>
    public int? MentionCount { get; init; }
}

/// <summary>
/// A link suggestion (glossary): <c>Text</c> sits at <c>Start</c> for <c>Length</c> characters
/// (UTF-16) of the note's text, and matched <c>Entry</c>. Accepting it is the author's own
/// <c>PUT notes/{id}</c> with the span rewritten to a mention.
/// </summary>
public record LinkSuggestionResponse
{
    public required int Start { get; init; }
    public required int Length { get; init; }
    public required string Text { get; init; }
    public required EntrySummaryResponse Entry { get; init; }
    public required double Similarity { get; init; }
}

/// <summary>
/// The caller's loose ends (19b.3, design §5), with link suggestions for unlinked notes. Only
/// what the caller can resolve is listed: their own notes, and the entries they can edit. No
/// paging: it is one member's to-do list. Nothing is stored, so nothing can be dismissed;
/// resolving is an ordinary edit, and the loose end leaves when its rule stops matching.
/// </summary>
public class GetLooseEnds(IDocumentSession session, EntryMatcher matcher) : Endpoint<GetLooseEndsRequest, LooseEndsResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/loose-ends");
    }

    public override async Task HandleAsync(GetLooseEndsRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        var read = await LooseEnds.For(session, req.CampaignId, member, ct);
        var items = req.SessionId is { } only
            ? read.Items.Where(i => i.SessionId == only).ToList()
            : read.Items;

        var notes = items.Select(i => i.Note).OfType<SessionNote>().ToList();
        var suggestions = await LooseEnds.SuggestLinks(matcher, session, req.CampaignId, member, notes, ct);

        var sessionIds = items.Select(i => i.SessionId).OfType<Guid>().Distinct().ToArray();
        var numbers = sessionIds.Length == 0
            ? new Dictionary<Guid, int>()
            : (await session.LoadManyAsync<Session>(ct, sessionIds)).ToDictionary(s => s.Id, s => s.Number);

        await SendAsync(new LooseEndsResponse
        {
            Items = items.Select(i => new LooseEndResponse
            {
                Kind = i.Kind,
                SessionId = i.SessionId,
                SessionNumber = i.SessionId is { } id && numbers.TryGetValue(id, out var number) ? number : null,
                Note = i.Note is null ? null : SessionNoteResponse.From(i.Note),
                Suggestions = i.Note is null
                    ? []
                    : [.. suggestions.GetValueOrDefault(i.Note.Id, [])
                        .Where(s => read.Entries.ContainsKey(s.EntryId))
                        .Select(s => new LinkSuggestionResponse
                        {
                            Start = s.Span.Start,
                            Length = s.Span.Length,
                            Text = s.Span.Text,
                            Entry = EntrySummaryResponse.From(read.Entries[s.EntryId]),
                            Similarity = s.Similarity,
                        })],
                Entry = i.Entry is null ? null : EntrySummaryResponse.From(i.Entry),
                MentionCount = i.Entry is null ? null : i.MentionCount,
            }).ToArray(),
        }, cancellation: ct);
    }
}
