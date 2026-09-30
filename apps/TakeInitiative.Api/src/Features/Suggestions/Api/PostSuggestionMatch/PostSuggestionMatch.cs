using FastEndpoints;
using FluentValidation;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Suggestions;

public record PostSuggestionMatchRequest
{
    public Guid CampaignId { get; init; }
    /// <summary>The spans the suggestion model found in the caller's notes, at most 50.</summary>
    public required SuggestionSpanRequest[] Spans { get; init; }
}

/// <summary>A span's text as the note has it (any case), and the kind the model gave it.</summary>
public record SuggestionSpanRequest
{
    /// <summary>1–80 characters.</summary>
    public required string Text { get; init; }
    /// <summary>The model's label. Not used to match: only the create path needs it, and the web has it.</summary>
    public EntryKind? Kind { get; init; }
}

public record PostSuggestionMatchResponse
{
    /// <summary>One per span, in request order: the entry it names, or null for none the caller can see.</summary>
    public required SuggestionMatchResponse?[] Matches { get; init; }
}

public record SuggestionMatchResponse
{
    /// <summary>The entry, with its own name: a span "rellan" answers "Rellan Ashvale".</summary>
    public required EntrySummaryResponse Entry { get; init; }
    public required double Similarity { get; init; }
}

public class PostSuggestionMatchRequestValidator : Validator<PostSuggestionMatchRequest>
{
    public const int MaxSpans = 50;

    public PostSuggestionMatchRequestValidator()
    {
        RuleFor(x => x.Spans).NotNull()
            .Must(spans => spans is null || spans.Length <= MaxSpans)
            .WithMessage($"At most {MaxSpans} spans can be matched at once.");
        RuleForEach(x => x.Spans).ChildRules(span =>
        {
            span.RuleFor(s => s.Text)
                .Must(t => !string.IsNullOrWhiteSpace(t) && t.Trim().Length <= SuggestionEdit.SpanMaxLength)
                .WithMessage($"A span is 1 to {SuggestionEdit.SpanMaxLength} characters.");
            span.RuleFor(s => s.Kind).IsInEnum();
        });
    }
}

/// <summary>
/// "Is this span an existing entry?" for the suggestion model's spans (23c.2, §11a: match before
/// creating). Every span goes to the entry matcher in one call, which folds case and accents,
/// applies the caller's visibility and drops merged entries, and <see cref="LinkSpans.Accepts"/>
/// decides exactly as for link suggestions (19b). So a hidden entry is never a match, and its
/// null looks the same as "no such entry". Nothing is stored, and the spans are not logged.
/// </summary>
public class PostSuggestionMatch(IDocumentSession session, EntryMatcher matcher)
    : Endpoint<PostSuggestionMatchRequest, PostSuggestionMatchResponse>
{
    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/suggestions/match");
    }

    public override async Task HandleAsync(PostSuggestionMatchRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        var texts = req.Spans.Select(s => s.Text.Trim()).ToList();
        var found = texts.Count == 0
            ? []
            : await matcher.MatchAsync(session, req.CampaignId, member, texts, LooseEnds.LooseEnds.MatchOptions, ct);
        var picks = texts
            .Select((text, i) => found[i].FirstOrDefault(m => LinkSpans.Accepts(text, m)))
            .ToList();

        // A List, not an array: Marten cannot translate C# 14's span-based Contains on an array.
        var ids = picks.OfType<EntryMatch>().Select(m => m.EntryId).Distinct().ToList();
        var entries = ids.Count == 0
            ? new Dictionary<Guid, Entry>()
            : (await session.Query<Entry>().Listed(req.CampaignId, member).Where(e => ids.Contains(e.Id)).ToListAsync(ct))
                .ToDictionary(e => e.Id);

        await SendAsync(new PostSuggestionMatchResponse
        {
            Matches = [.. picks.Select(m => m is not null && entries.TryGetValue(m.EntryId, out var entry)
                ? new SuggestionMatchResponse { Entry = EntrySummaryResponse.From(entry), Similarity = m.Similarity }
                : null)],
        }, cancellation: ct);
    }
}
